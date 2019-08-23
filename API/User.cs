using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VP_Functions.Models;

namespace VP_Functions.API
{
  public static class User
  {
    [FunctionName("GetUser")]
    public static async Task<IActionResult> GetUser(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "user/{id:int}")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log, int id, FancyConn conn = null)
    {
      string email = principal?.FindFirst(ClaimTypes.Email)?.Value;
      if (email  == null)
        return Respond.BadRequest("Not logged in.");
      if (conn == null)
        conn = new FancyConn();

      try
      {
        var role = await conn.GetRole(email);
        if (role < Role.Executive)
        {
          log.LogWarning($"[user] Unauthorized attempt by {email} to access record {id}");
          return Respond.Error<object>($"Unauthorized.", statusCode: HttpStatusCode.Unauthorized);
        }

        var cols = new List<string>() {
        "user_id", "role_id",
        "first_name", "last_name",
        "email", "phone_1", "phone_2", "school",
        "title", "bio", "pic", "show_exec"
        };
        var reader = await conn.Reader(
          $@"SELECT TOP 1 [{string.Join("], [", cols)}] FROM [user] WHERE [user_id] = @id",
          new Dictionary<string, object>() { { "id", id } });
        if (reader == null) return Respond.Error("Unable to fetch user.", conn.lastError);

        if (reader.HasRows)
        {
          reader.Read();
          var data = new Dictionary<string, object>();
          for (int i = 0; i < cols.Count; i++)
            data.Add(cols[i], reader[i]);
          reader.Close();
          return Respond.Ok("Retrieved user successfully.", data);
        }
        else
        {
          return Respond.NotFound("User not found.");
        }
      }
      catch (Exception e)
      {
        return Respond.Error(data: e);
      }
      finally
      {
        conn.Dispose();
      }
    }

    [FunctionName("GetCurrentUser")]
    public static async Task<IActionResult> GetCurrentUser(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log, FancyConn conn = null)
    {
      string email = principal?.FindFirst(ClaimTypes.Email)?.Value;
      if (email == null)
        return Respond.BadRequest("Not logged in.");
      if (conn == null)
        conn = new FancyConn();

      try
      {
        bool created = false;
        // use role check to see if user exists already
        bool exists = await conn.GetRole(email) != null;
        if (!exists)
        {
          var newData = new Dictionary<string, object>()
          {
            { "first", principal.FindFirst(ClaimTypes.GivenName)?.Value },
            { "last", principal.FindFirst(ClaimTypes.Surname)?.Value },
            { "email", email }
          };
          var newID = await conn.Scalar(@"INSERT INTO [user] (first_name, last_name, email, role_id)
            VALUES (@first, @last, @email, 1); SELECT SCOPE_IDENTITY()", newData);
          if (newID == null) return Respond.Error("Unable to create user.", conn.lastError);
          created = true;
        }

        var user = new Dictionary<string, object>();
        // get user data
        var cols = new List<string>() {
        "user_id", "role_id",
        "first_name", "last_name",
        "email", "phone_1", "phone_2", "school",
        "title", "bio", "pic", "show_exec"
        };
        var reader = await conn.Reader(
          $@"SELECT TOP 1 [{string.Join("], [", cols)}] FROM [user] WHERE [email] = @email",
          new Dictionary<string, object>() { { "email", email } });
        if (reader?.HasRows != true) return Respond.Error("Unable to fetch user.", conn.lastError);
        reader.Read();
        foreach (string col in cols)
          user.Add(col, reader[col]);
        reader.Close();

        // get confirm levels
        var userShifts = new List<UserShift>();
        reader = await conn.Reader(@"SELECT [user_shift_id], [hours],
          [confirm_level_id], [confirm_level], [confirm_description], [letter],
          [shift_id], [shift_num], [start_time], [end_time], [hours], [event_id], [name]
          FROM [vw_user_shift] WHERE [user_id] = @id",
          new Dictionary<string, object>() { { "id", user["user_id"] } });
        if (reader == null) return Respond.Error("Unable to fetch attendance statuses.", conn.lastError);
        while (reader.Read())
        {
          var us = new UserShift((int)reader["user_shift_id"], (string)reader["hours"],
            (string)reader["letter"], (int)reader["event_id"], (string)reader["name"])
          {
            confirm_level = new ConfirmLevel((int)reader["confirm_level_id"],
            (string)reader["confirm_level"], (string)reader["confirm_description"]),
            shift = new RawShift((int)reader["shift_id"], (int)reader["shift_num"],
            (string)reader["start_time"], (string)reader["end_time"], (string)reader["date"],
            reader["meals"].ToString().Split(","))
          };
          userShifts.Add(us);
        }
        reader.Close();

        return Respond.Ok("Retrieved user successfully.", new
        {
          user,
          created,
          userShifts
        });
      }
      catch (Exception e)
      {
        return Respond.Error(data: e);
      }
      finally
      {
        conn.Dispose();
      }
    }
  }
}
