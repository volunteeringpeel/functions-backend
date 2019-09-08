using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using VP_Functions.Helpers;
using Newtonsoft.Json.Linq;

namespace VP_Functions.API
{
  public static class MailList
  {
    /// <summary>
    /// Get a list of all mailing lists
    /// </summary>
    public static async Task<IActionResult> GetAll()
    {
      var (err, reader) = await FancyConn.Shared.Reader(@"SELECT
        [mail_list_id], [display_name], [description], [first_name], [last_name], [email]
      FROM [vw_user_mail_list]");
      if (err) return Response.Error("Unable to get mail lists.", FancyConn.Shared.lastError);

      var mailLists = new JArray();
      // list of members for each mail_list_id
      var members = new Dictionary<int, JArray>();
      while (reader.Read())
      {
        var id = (int)reader[0];
        var member = new JObject()
          {
            { "first_name", (string)reader[3] },
            { "last_name", (string)reader[4] },
            { "email", (string)reader[5] },
          };
        if (members.ContainsKey(id))
        {
          members[id].Add(member);
        }
        else
        {
          mailLists.Add(new JObject()
          {
            { "mail_list_id", id },
            { "display_name", (string)reader[1] },
            { "description", (string)reader[2] },
          });
          members.Add(id, new JArray(member));
        }
      }

      return Response.Ok("Got mail lists successfully.", mailLists);
    }

    /// <summary>
    /// Update metadata of a mail list by ID, create if not exists
    /// </summary>
    /// <param name="id">ID of mail list to modify, use -1 to always create.</param>
    public static async Task<IActionResult> CreateOrUpdate(HttpRequest req, ClaimsPrincipal principal, ILogger log, int id)
    {
      var body = await req.GetBodyParameters();
      var (query, param) = FancyConn.MakeUpsertQuery("mail_list", "mail_list_id", id,
        new List<string>() { "display_name", "description" }, body);
      var (err, newId) = await FancyConn.Shared.Scalar(query, param);
      if (err) return Response.Error("Unable to edit mail list record.", FancyConn.Shared.lastError);

      return Response.Ok($"{(newId == null ? "Updated" : "Created")} mail list successfully.");
    }

    /// <summary>
    /// Delete a mail list by ID
    /// </summary>
    /// <param name="id">ID of mail list to delete</param>
    public static async Task<IActionResult> Delete(HttpRequest req, ClaimsPrincipal principal, ILogger log, int id)
    {
      var (err, rows) = await FancyConn.Shared.NonQuery("DELETE FROM [mail_list] WHERE [mail_list_id] = @id",
        new Dictionary<string, object>() { { "id", id } });
      if (err || rows != 1) return Response.Error("Unable to delete mail list.", FancyConn.Shared.lastError);

      return Response.Ok("Deleted mail list successfully.");
    }

    /// <summary>
    /// Sign up a given email for a mail list
    /// </summary>
    /// <param name="id">ID of mail list to sign up for</param>
    public static async Task<IActionResult> Signup(HttpRequest req, ClaimsPrincipal principal, ILogger log, int id)
    {
      var body = await req.GetBodyParameters();
      var email = body["email"].Value<string>();
      var query = @"
        IF NOT EXISTS (SELECT [user_id] FROM [user] WHERE [email] = @email) BEGIN
          INSERT INTO [user]([email]) VALUES (@email)
          INSERT INTO [user_mail_list]([user_id],[mail_list_id])
            SELECT SCOPE_IDENTITY() AS [user_id], @id AS [mail_list_id]
        END
        ELSE BEGIN
          INSERT INTO [user_mail_list]([user_id],[mail_list_id])
            SELECT [user_id], @id AS [mail_list_id] FROM [user] WHERE email = @email
        END";
      var (err, rows) = await FancyConn.Shared.NonQuery(query,
        new Dictionary<string, object>() { { "id", id }, { "email", email } });
      if (err) return Response.Error("Unable to sign up for mail list.", FancyConn.Shared.lastError);

      return Response.Ok("Signed up for mail list successfully.");
    }
  }
}
