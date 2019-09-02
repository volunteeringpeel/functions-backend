using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using VP_Functions.Helpers;

namespace VP_Functions.API
{
  class Signup
  {
    /// <summary>
    /// Signup for specific shift IDs
    /// </summary>
    public static async Task<IActionResult> Run(HttpRequest req, ClaimsPrincipal principal)
    {
      var (err, id) = await FancyConn.Shared.Scalar("SELECT [user_id] FROM [user] WHERE [email] = @email",
          new Dictionary<string, object>() { { "email", principal.FindFirst(ClaimTypes.Email).Value } });
      if (err) return Response.Error("Unable to get user data.", FancyConn.Shared.lastError);
      if (id == null) return Response.NotFound("Unable to find user.");

      var body = await req.GetBodyParameters();
      var shifts = (JArray)body["shifts"];
      if (!shifts.HasValues) return Response.BadRequest("No shifts selected.");
      var info = (string)body["add_info"];

      var inserts = new List<string>();
      var queryParams = new Dictionary<string, object>
        {
          { "id", id },
          { "info", info },
        };
      for (var i = 0; i < shifts.Count; i++)
      {
        inserts.Add($"(@id, @s{i}, @info)");
        queryParams.Add($"s{i}", shifts[i].ToString());
      }

      var query = $"INSERT INTO [user_shift]([user_id], [shift_id], [add_info]) VALUES {string.Join(", ", inserts)}";
      (err, _) = await FancyConn.Shared.NonQuery(query, queryParams);
      if (err) return Response.Error("Unable to sign up for shifts.", FancyConn.Shared.lastError);

      return Response.Ok("Signed up successfully.");
    }
  }
}
