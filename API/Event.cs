using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using VP_Functions.Models;

namespace VP_Functions.API
{
  public static class Event
  {
    /// <summary>
    /// Get multiple events.
    /// Use filter query parameter to specify which events:
    /// - "active" (public) for all events marked active
    /// - "nonarchived" (private) for all events which are not archived
    /// - "all" (private) for all events
    /// </summary>
    [FunctionName("GetAllEvents")]
    public static async Task<IActionResult> GetAllEvents(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log)
    {
      IDictionary<string, string> queryParams = req.GetQueryParameterDictionary();
      var filter = queryParams.ContainsKey("filter") ? queryParams["filter"] : "active";
      if (filter != "active" && filter != "nonarchived" && filter != "all")
        return Response.BadRequest("Bad query parameter filter.");
      FancyConn.EnsureShared();

      try
      {
        // authorization enforcement
        if (filter != "active")
        {
          var role = await FancyConn.Shared.GetRole(principal?.FindFirst(ClaimTypes.Email)?.Value);
          if (role < Role.Executive)
            return Response.Error<object>("Unauthorized.", statusCode: HttpStatusCode.Unauthorized);
        }

        var data = new JArray();
        var cols = new List<string>() { "event_id", "name", "address", "transport", "description", "add_info" };

        // set filters
        var where = " WHERE [active] = 1";
        if (filter == "all") where = "";
        if (filter == "nonarchived") where = " WHERE [archived] = 0";
        // get active/archived data if private endpoint
        if (filter != "current") cols.AddRange(new string[] { "active", "archived" });

        // get events
        var (err, reader) = await FancyConn.Shared.Reader($"SELECT [{string.Join("], [", cols)}] FROM [event]" + where);
        if (err) Response.Error("Unable to retrieve events.", FancyConn.Shared.lastError);
        while (reader.Read())
        {
          var ev = new JObject(
            from col in cols
            select new JProperty(col, reader[col]));
          ev.Add("shifts", new JArray());
          data.Add(ev);
        }
        reader.Close();

        // get shifts
        var shiftCols = new List<string>() { "shift_id", "event_id", "shift_num", "start_time", "end_time", "meals", "spots_taken", "max_spots", "notes" };
        if (filter != "current") shiftCols.AddRange(new string[] { "active", "archived" });

        (err, reader) = await FancyConn.Shared.Reader($"SELECT [{string.Join("], [", shiftCols)}] FROM [vw_shift]" + where);
        if (err) return Response.Error($"Unable to retrieve shifts.", FancyConn.Shared.lastError);
        while (reader.Read())
        {
          var ev = data.SelectToken($"$[?(@.event_id == {reader["event_id"]})]");
          ev["shifts"].Value<JArray>().Add(new JObject(
            from col in shiftCols
            select new JProperty(col, reader[col])));
        }

        return Response.Ok("Retrieved events successfully.", data);
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
    /// Update or create an event, along with its shifts.
    /// </summary>
    /// <param name="id">ID of event to update, -1 to create</param>
    [FunctionName("SetEvent")]
    public static async Task<IActionResult> SetEvent(
      [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "event/{id:int}")] HttpRequest req,
      [Blob("website-upload/hours-letters", FileAccess.ReadWrite)] CloudBlobDirectory blobDirectory,
      ClaimsPrincipal principal, ILogger log, int id)
    {
      //string email = principal?.FindFirst(ClaimTypes.Email)?.Value;
      //if (email == null)
      //  return Response.BadRequest("Not logged in.");
      var body = await req.GetBodyParameters();
      var deleteShifts = body["deleteShifts"]?.Value<JArray>().Select(s => (int)s);
      var shifts = body["shifts"]?.Values<JObject>();

      FancyConn.EnsureShared();

      try
      {
        //var role = await FancyConn.Shared.GetRole(email);
        //if (role < Role.Executive)
        //{
        //  log.LogWarning($"[event] Unauthorized attempt by {email} to edit record {id}");
        //  return Response.Error<JToken>($"Unauthorized.", statusCode: HttpStatusCode.Unauthorized);
        //}

        var letter = req.Form.Files.GetFile("letter");
        if (letter != null)
        {
          var blob = blobDirectory.GetBlockBlobReference(letter.FileName);
          await blob.UploadFromStreamAsync(letter.OpenReadStream());
        }

        var eventCols = new string[] { "name", "description", "transport", "address", "add_info", "letter" }.ToList();
        var (eventQuery, eventParams) = FancyConn.MakeUpsertQuery("event", "event_id", id, eventCols, body);
        var (err, newId) = await FancyConn.Shared.Scalar(eventQuery, eventParams);
        if (err) return Response.Error("Unable to update event.", FancyConn.Shared.lastError);

        if (deleteShifts.Count() > 0)
        {
          var delQuery = $"DELETE FROM [shifts] WHERE [shift_id] IN ({string.Join(", ", deleteShifts.Map(x => $"@{x}"))})";
          var delParams = deleteShifts.ToDictionary(x => $"p{x}", x => (object)x);
          (err, _) = await FancyConn.Shared.NonQuery(delQuery, delParams);
          if (err) return Response.Error("Unable to delete shifts.", FancyConn.Shared.lastError);
        }

        if (shifts.Count() > 0)
        {
          var errors = await Task.WhenAll(shifts.Select<JObject, Task<int?>>(async s =>
          {
            var shiftCols = new string[] { "event_id", "shift_num", "max_spots", "start_time", "end_time", "meals", "notes" }.ToList();
            s["event_id"] = new JValue(newId ?? id);
            var (shiftQuery, shiftParams) = FancyConn.MakeUpsertQuery("shift", "shift_id", (int)s["shift_id"], shiftCols, s);
            var (error, _) = await FancyConn.Shared.NonQuery(shiftQuery, shiftParams);
            if (error) return (int)s["shift_num"];
            return null;
          }));
          var failed = string.Join(", ", errors.Where(x => x != null).ToList());
          if (failed.Length > 0) return Response.Error($"Unable to update shift {failed}", FancyConn.Shared.lastError);
        }

        return Response.Ok("Updated event successfully.");
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
    /// Delete an event, including all shifts
    /// </summary>
    /// <param name="id">ID of event to delete</param>
    [FunctionName("DeleteEvent")]
    public static async Task<IActionResult> DeleteEvent(
      [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "event/{id:int}")] HttpRequest req,
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

        var (err, _) = await FancyConn.Shared.NonQuery("DELETE FROM [event] WHERE [event_id] = @id",
          new Dictionary<string, object>() { { "id", id } });
        if (err) return Response.Error("Unable to delete event", FancyConn.Shared.lastError);

        return Response.Ok("Deleted event successfully.");
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
