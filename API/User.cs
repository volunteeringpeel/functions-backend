using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VP_Functions.Models;
using System.Collections.Generic;

namespace VP_Functions.API
{
  public static class User
  {
    [FunctionName("GetUser")]
    public static async Task<IActionResult> GetUser(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "user/{id:int}")] HttpRequest req,
      ILogger log, int id)
    {
      log.LogInformation($"[user] [get] {id}");
      var conn = new FancyConn();

      var cols = new List<string>() {
        "user_id", "role_id",
        "first_name", "last_name",
        "email", "phone_1", "phone_2", "school",
        "title", "bio", "pic", "show_exec"
        };
      var reader = await conn.Reader(
        $@"SELECT TOP 1 {string.Join(",", cols)} FROM [user] WHERE user_id = @id",
        new Dictionary<string, object>() { { "id", id } });
      if (reader == null) return Response.Error(500, "Unable to fetch user.", conn.lastError);

      if (reader.HasRows)
      {
        reader.Read();
        var data = new Dictionary<string, object>();
        for (int i = 0; i < cols.Count; i++)
          data.Add(cols[i], reader[i]);
        return Response.Ok(200, "Retrieved user successfully.", data);
      }
      else
      {
        return Response.NotFound("User not found.");
      }
    }
  }
}
