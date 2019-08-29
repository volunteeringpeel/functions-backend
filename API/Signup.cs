using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Build.Framework;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using VP_Functions.Models;

namespace VP_Functions.API
{
  class Signup
  {
    /// <summary>
    /// Signup for specific shift IDs
    /// </summary>
    [FunctionName("Signup")]
    public static async Task<IActionResult> Run(
      [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "signup")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log)
    {
      string email = principal?.FindFirst(ClaimTypes.Email)?.Value;
      if (email == null)
        return Response.BadRequest("Not logged in.");
      var body = await req.GetBodyParameters();

      FancyConn.EnsureShared();

      try
      {
        var (err, id) = await FancyConn.Shared.Scalar("SELECT [user_id] FROM [user] WHERE [email] = @email",
          new Dictionary<string, object>() { { "email", email } });
        if (err) return Response.Error("Unable to get user data.", FancyConn.Shared.lastError);
        if (id == null) return Response.NotFound("Unable to find user.");

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
