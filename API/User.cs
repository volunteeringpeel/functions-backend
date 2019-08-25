using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VP_Functions.Models;

namespace VP_Functions.API
{
  public static class User
  {
    /// <summary>
    /// Get a single user.
    /// </summary>
    /// <param name="id">ID of user to get</param>
    [FunctionName("GetUser")]
    public static async Task<IActionResult> GetUser(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "user/{id:int}")] HttpRequest req,
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
          log.LogWarning($"[user] Unauthorized attempt by {email} to access record {id}");
          return Response.Error<JToken>($"Unauthorized.", statusCode: HttpStatusCode.Unauthorized);
        }

        var cols = new List<string>() {
        "user_id", "role_id",
        "first_name", "last_name",
        "email", "phone_1", "phone_2", "school",
        "title", "bio", "pic", "show_exec"
        };
        var (err, reader) = await FancyConn.Shared.Reader(
          $@"SELECT TOP 1 [{string.Join("], [", cols)}] FROM [user] WHERE [user_id] = @id",
          new Dictionary<string, object>() { { "id", id } });
        if (err) return Response.Error("Unable to fetch user.", FancyConn.Shared.lastError);

        if (reader.HasRows)
        {
          reader.Read();
          var data = new JObject();
          foreach (var col in cols)
            data.Add(col, JToken.FromObject(reader[col]));
          reader.Close();
          return Response.Ok("Retrieved user successfully.", data);
        }
        else
        {
          return Response.NotFound("User not found.");
        }
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
    /// Like <see cref="GetUser"/>, but for the currently logged in user.
    /// </summary>
    [FunctionName("GetCurrentUser")]
    public static async Task<IActionResult> GetCurrentUser(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log)
    {
      string email = principal?.FindFirst(ClaimTypes.Email)?.Value;
      if (email == null)
        return Response.BadRequest("Not logged in.");
      FancyConn.EnsureShared();

      try
      {
        bool created = false;
        // use role check to see if user exists already
        bool exists = await FancyConn.Shared.GetRole(email) != null;
        if (!exists)
        {
          var newData = new Dictionary<string, object>()
          {
            { "first", principal.FindFirst(ClaimTypes.GivenName)?.Value },
            { "last", principal.FindFirst(ClaimTypes.Surname)?.Value },
            { "email", email }
          };
          var (error, newID) = await FancyConn.Shared.Scalar(@"INSERT INTO [user] (first_name, last_name, email, role_id)
            VALUES (@first, @last, @email, 1); SELECT SCOPE_IDENTITY()", newData);
          if (error) return Response.Error("Unable to create user.", FancyConn.Shared.lastError);
          created = true;
        }

        // get user data
        var cols = new List<string>() {
        "user_id", "role_id",
        "first_name", "last_name",
        "email", "phone_1", "phone_2", "school",
        "title", "bio", "pic", "show_exec"
        };
        var (err, reader) = await FancyConn.Shared.Reader(
          $@"SELECT TOP 1 [{string.Join("], [", cols)}] FROM [user] WHERE [email] = @email",
          new Dictionary<string, object>() { { "email", email } });
        if (err) return Response.Error("Unable to fetch user.", FancyConn.Shared.lastError);
        if (!reader.Read()) return Response.Error<object>("User does not exist.", statusCode: HttpStatusCode.NotFound);
        var user = new JObject();
        foreach (var col in cols)
          user.Add(col, JToken.FromObject(reader[col]));
        reader.Close();

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
    /// Update a given user.
    /// </summary>
    /// <param name="id">ID of user to set</param>
    [FunctionName("SetUser")]
    public static async Task<IActionResult> SetUser(
      [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "user/{id:int}")] HttpRequest req,
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
          log.LogWarning($"[user] Unauthorized attempt by {email} to modify record {id}");
          return Response.Error<object>($"Unauthorized.", statusCode: HttpStatusCode.Unauthorized);
        }

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        dynamic body = JsonConvert.DeserializeObject(requestBody);

        var cols = new string[] { "first_name", "last_name",
          "email", "phone_1", "phone_2", "school", "role_id", "bio", "title", "pic", "show_exec" }.ToList();

        var equals = new List<string>();
        var parameters = new Dictionary<string, object>() { { "id", id } };
        int i = 0;
        foreach (JProperty kv in body)
        {
          string col = kv.Name;
          JToken value = kv.Value;
          if (!cols.Contains(col))
            return Response.Error($"Passed unsupported column {col}.");

          equals.Add($"[{col}] = @p{i.ToString()}");
          parameters.Add($"@p{i.ToString()}", value?.ToString());
          i++;
        }

        var (err, rows) = await FancyConn.Shared.NonQuery($"UPDATE [user] SET {string.Join(",", equals)} WHERE [user_id] = @id", parameters);
        if (err) return Response.Error("Unable to update user data.", FancyConn.Shared.lastError);
        if (rows != 1) return Response.NotFound("Unable to find user.");
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
