using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using VP_Functions.Models;

namespace VP_Functions.API
{
  public static class User
  {
    private enum ReqType { ID, New, Current }

    /// <summary>
    /// Get a single user.
    /// </summary>
    /// <param name="type">How to select the user to get</param>
    /// <param name="id">ID of user to get</param>
    private static async Task<IActionResult> GetUser(
      HttpRequest req, ClaimsPrincipal principal, ILogger log, ReqType type, int id = -1)
    {
      string email = principal?.FindFirst(ClaimTypes.Email)?.Value;
      if (email == null)
        return Response.BadRequest("Not logged in.");
      FancyConn.EnsureShared();

      try
      {
        var role = await FancyConn.Shared.GetRole(email);

        if (type != ReqType.Current && role < Role.Executive)
        {
          log.LogWarning($"[user] Unauthorized attempt by {email} to access record {id}");
          return Response.Error<JToken>($"Unauthorized.", statusCode: HttpStatusCode.Unauthorized);
        }

        bool created = false;
        // use role check to see if user exists already
        if (role != Role.None || type == ReqType.New)
        {
          var newData = new Dictionary<string, object>()
          {
            { "first", principal.FindFirst(ClaimTypes.GivenName)?.Value },
            { "last", principal.FindFirst(ClaimTypes.Surname)?.Value },
            { "email", email }
          };
          var (error, _) = await FancyConn.Shared.Scalar(@"INSERT INTO [user] (first_name, last_name, email, role_id)
            VALUES (@first, @last, @email, 1); SELECT SCOPE_IDENTITY()", newData);
          if (error) return Response.Error("Unable to create user.", FancyConn.Shared.lastError);
          created = true;
        }

        var cols = new List<string>()
        {
          "user_id", "role_id",
          "first_name", "last_name",
          "email", "phone_1", "phone_2", "school",
          "title", "bio", "pic", "show_exec"
        };

        // use email or ID based on request type
        var (err, reader) = (type == ReqType.ID)
          ? await FancyConn.Shared.Reader(
            $@"SELECT TOP 1 [{string.Join("], [", cols)}] FROM [user] WHERE [user_id] = @id",
            new Dictionary<string, object>() { { "id", id } })
          : await FancyConn.Shared.Reader(
            $@"SELECT TOP 1 [{string.Join("], [", cols)}] FROM [user] WHERE [email] = @email",
            new Dictionary<string, object>() { { "email", email } });
        if (err) return Response.Error("Unable to fetch user.", FancyConn.Shared.lastError);

        var user = new JObject();
        if (reader.HasRows)
        {
          reader.Read();
          foreach (var col in cols)
            user.Add(col, JToken.FromObject(reader[col]));
          reader.Close();
        }
        else
        {
          return Response.NotFound("User not found.");
        }

        // get confirm levels
        var userShifts = new JArray();
        (err, reader) = await FancyConn.Shared.Reader(@"SELECT [user_shift_id], [hours],
          [confirm_level_id], [confirm_level], [confirm_description], [letter],
          [shift_id], [shift_num], [start_time], [end_time], [event_id], [name]
          FROM [vw_user_shift] WHERE [user_id] = @id",
          new Dictionary<string, object>() { { "id", user["user_id"].Value<string>() } });
        if (err) return Response.Error("Unable to fetch attendance statuses.", FancyConn.Shared.lastError);
        while (reader.Read())
        {
          var us = new JObject(
            new JProperty("user_shift_id", reader.GetInt32(0)),
            new JProperty("hours", reader.GetString(1)),
            new JProperty("confirm_level", new JObject(
              new JProperty("id", reader.GetInt32(2)),
              new JProperty("name", reader.GetString(3)),
              new JProperty("description", reader.GetString(4)))),
            new JProperty("letter", reader.GetString(5)),
            new JProperty("shift", new JObject(
              new JProperty("shift_id", reader.GetInt32(6)),
              new JProperty("shift_num", reader.GetInt32(7)),
              new JProperty("start_time", reader.GetString(8)),
              new JProperty("end_time", reader.GetString(9)))),
            new JProperty("parentEvent", new JObject(
              new JProperty("event_id", reader.GetString(10)),
              new JProperty("name", reader.GetString(11)))));
          userShifts.Add(us);
        }
        reader.Close();

        // get mail list subscriptions
        (err, reader) = await FancyConn.Shared.Reader(
          @"SELECT
            m.[mail_list_id] as [mail_list_id],
            m.[display_name] as [display_name],
            m.[description] as [description],
            NOT ISNULL(uml.[user_mail_list_id]) [subscribed]
          FROM [user] u
          JOIN [mail_list] m
          LEFT JOIN [user_mail_list] uml ON uml.[user_id] = u.[user_id] AND uml.[mail_list_id] = m.[mail_list_id]
          WHERE u.user_id = @id",
          new Dictionary<string, object>() { { "id", (int)user["user_id"] } });
        if (err) return Response.Error("Unable to get mail list subscriptions.", FancyConn.Shared.lastError);

        var mailLists = new JArray();
        while (reader.Read())
          mailLists.Add(new JObject(from c in reader.GetColumnSchema()
                                    select new JProperty(c.ColumnName, reader[(int)c.ColumnOrdinal])));
        user.Add("mail_lists", mailLists);

        return Response.Ok("Retrieved user successfully.", new { user, created, userShifts });
      }
      catch (Exception e)
      {
        return Response.Error(data: e);
      }
      finally
      {
        FancyConn.Shared.Dispose();
      }
    }

    /// <summary>
    /// Fetch a user with a given ID
    /// </summary>
    /// <param name="id">ID of user to get</param>
    [FunctionName("GetUserByID")]
    public static async Task<IActionResult> GetUserByID(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "user/{id:int}")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log, int id)
      => await GetUser(req, principal, log, ReqType.Current, id);

    /// <summary>
    /// Fetch the currently logged in user.
    /// </summary>
    [FunctionName("GetCurrentUser")]
    public static async Task<IActionResult> GetCurrentUser(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log)
      => await GetUser(req, principal, log, ReqType.Current);

    /// <summary>
    /// Create a new user or update a current or given user.
    /// </summary>
    /// <param name="type">How to select the user to update</param>
    /// <param name="id">ID of user to update (if in ID mode)</param>
    private static async Task<IActionResult> SetUser(
      HttpRequest req, CloudBlobDirectory blobDirectory, ClaimsPrincipal principal, ILogger log,
      ReqType type = ReqType.ID, int id = -1)
    {
      string email = principal?.FindFirst(ClaimTypes.Email)?.Value;
      if (email == null)
        return Response.BadRequest("Not logged in.");
      var body = await req.GetBodyParameters();

      FancyConn.EnsureShared();

      try
      {
        var role = await FancyConn.Shared.GetRole(email);
        // generate error if not an exec AND not updating self
        if (type != ReqType.Current && role < Role.Executive)
        {
          log.LogWarning($"[user] Unauthorized attempt by {email} to modify record {id}");
          return Response.Error<object>($"Unauthorized.", statusCode: HttpStatusCode.Unauthorized);
        }

        var userId = id;
        if (type == ReqType.Current)
        {
          var (err, currentId) = await FancyConn.Shared.Scalar("SELECT [user_id] FROM [user] WHERE [email] = @email",
            new Dictionary<string, object>() { { "email", email } });
          if (err) return Response.Error("Error finding user in database.", FancyConn.Shared.lastError);
          if (currentId == null) return Response.Error("User does not exist. This should not happen");
          userId = (int)currentId;
        }

        var validCols = new List<string> { "first_name", "last_name",
          "phone_1", "phone_2", "school", "role_id", "bio", "title", "show_exec" };
        // cannot update email from user profile page
        if (type != ReqType.Current) validCols.Add("email");

        // handle exec picture
        var pic = req.Form.Files.GetFile("pic");
        if (pic != null)
        {
          var picStream = pic.OpenReadStream();
          var img = Image.Load(picStream);
          // resize to width of 350px while maintaining aspect ratio
          img.Mutate(i => i.Resize(0, 350));

          var blob = blobDirectory.GetBlockBlobReference(pic.FileName.WithTimestamp());
          await blob.UploadFromStreamAsync(pic.OpenReadStream());
          validCols.Add("pic");
          body.Add("pic", blob.Uri.ToString());
        }

        // handle body using email if CurrentUser, user_id if not
        var (userQuery, userParams) = FancyConn.MakeUpsertQuery("user", "user_id", userId, validCols, body);

        // ensure at least one valid column was passed
        if (!string.IsNullOrEmpty(userQuery))
        {
          var (err, newId) = await FancyConn.Shared.Scalar(userQuery, userParams);
          if (err) return Response.Error("Unable to update user.", FancyConn.Shared.lastError);
          // update ID for future queries if this is a create
          if (type == ReqType.New) userId = (int)newId;
        }

        // handle mail list signups
        var mailLists = body["mail_lists"]?.Value<JArray>();
        if (mailLists != null && mailLists.Count > 0)
        {
          var i = 0;
          var queryParams = new Dictionary<string, object>() { { "id", userId } };
          var toDelete = new List<string>();
          var query = new StringBuilder();

          foreach (var list in mailLists)
          {
            var listId = list["mail_list_id"].Value<int>();
            if (list["subscribed"].Value<bool>())
            {
              // wacky query to ensure that we don't get a bunch of duplicate key errors
              query.AppendLine(
                $@"BEGIN TRY
                  INSERT INTO [user_mail_list]([user_id], [mail_list_id]) VALUES (@id, @p{i})
                END TRY
                BEGIN CATCH
                  IF ERROR_NUMBER() != 2627 THROW -- a unique key constraint violation
                END CATCH;");
              queryParams.Add($"p{i.ToString()}", listId);
              i++;
            }
            else
            {
              toDelete.Add($"p{i.ToString()}");
              queryParams.Add($"p{i.ToString()}", listId);
              i++;
            }
          }
          query.AppendLine($"DELETE FROM [user_mail_list] WHERE [user_id] = @id AND [mail_list_id] IN ({string.Join(", ", toDelete)});");

          var (err, _) = await FancyConn.Shared.NonQuery(query.ToString(), queryParams);
          if (err) return Response.Error("Unable to update mail list subscriptions.", FancyConn.Shared.lastError);
        }

        return Response.Ok("User updated successfully.");
      }
      catch (Exception e)
      {
        return Response.Error(data: e);
      }
      finally
      {
        FancyConn.Shared.Dispose();
      }
    }

    /// <summary>
    /// Update a given user.
    /// </summary>
    /// <param name="id">ID of user to update</param>
    [FunctionName("SetUserByID")]
    public static async Task<IActionResult> SetUserByID(
      [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "user/{id:int}")] HttpRequest req,
      [Blob("website-upload/exec-photos", FileAccess.ReadWrite)] CloudBlobDirectory blobDirectory,
      ClaimsPrincipal principal, ILogger log, int id)
      => await SetUser(req, blobDirectory, principal, log, (id == -1) ? ReqType.New : ReqType.ID, id);

    /// <summary>
    /// Create a new user.
    /// </summary>
    [FunctionName("CreateUser")]
    public static async Task<IActionResult> CreateUser(
      [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "user")] HttpRequest req,
      [Blob("website-upload/exec-photos", FileAccess.ReadWrite)] CloudBlobDirectory blobDirectory,
      ClaimsPrincipal principal, ILogger log)
      => await SetUser(req, blobDirectory, principal, log, ReqType.New);

    /// <summary>
    /// Update the current user at a special endpoint (POST /me)
    /// </summary>
    [FunctionName("SetCurrentUser")]
    public static async Task<IActionResult> SetCurrentUser(
      [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "me")] HttpRequest req,
      [Blob("website-upload/exec-photos", FileAccess.ReadWrite)] CloudBlobDirectory blobDirectory,
      ClaimsPrincipal principal, ILogger log) =>
      await SetUser(req, blobDirectory, principal, log, ReqType.Current);

    /// <summary>
    /// Delete a user, including all related data
    /// </summary>
    /// <param name="id">ID of user to delete</param>
    [FunctionName("DeleteUser")]
    public static async Task<IActionResult> DeleteUser(
      [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "user/{id:int}")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log, int id)
    {
      string email = principal?.FindFirst(ClaimTypes.Email)?.Value;
      if (email == null)
        return Response.BadRequest("Not logged in.");
      FancyConn.EnsureShared();

      try
      {
        var role = await FancyConn.Shared.GetRole(email);
        if (role < Role.Executive)
        {
          log.LogWarning($"[event] Unauthorized attempt by {email} to delete record {id}");
          return Response.Error<JToken>($"Unauthorized.", statusCode: HttpStatusCode.Unauthorized);
        }

        var (err, rows) = await FancyConn.Shared.NonQuery("DELETE FROM [user] WHERE [user_id] = @id",
          new Dictionary<string, object>() { { "id", id } });
        if (err) return Response.Error("Unable to delete user", FancyConn.Shared.lastError);
        if (rows != 1) return Response.NotFound("Unable to find user.");

        return Response.Ok("Deleted user successfully.");
      }
      catch (Exception e)
      {
        return Response.Error(data: e);
      }
      finally
      {
        FancyConn.Shared.Dispose();
      }
    }
  }
}
