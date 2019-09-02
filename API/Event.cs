using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using VP_Functions.Helpers;

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
    public static async Task<IActionResult> GetAll(HttpRequest req, ILogger log)
    {
      var queryParams = req.GetQueryParameterDictionary();
      var filter = queryParams.ContainsKey("filter") ? queryParams["filter"] : "active";
      if (filter != "active" && filter != "nonarchived" && filter != "all")
        return Response.BadRequest("Bad query parameter filter.");
      // authorization enforcement
      if (filter != "active")
      {
        var email = req.HttpContext.User?.FindFirst(ClaimTypes.Email)?.Value;
        var role = await FancyConn.Shared.GetRole(email);
        if (role < Role.Executive)
        {
          log.LogWarning($"Unauthorized access by {email} to {req.Path}");
          return Response.Error<object>($"Unauthorized.", statusCode: HttpStatusCode.Unauthorized);
        }
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
          select new JProperty(col, reader[col]))
        {
          { "shifts", new JArray() }
        };
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

    /// <summary>
    /// Update or create an event, along with its shifts.
    /// </summary>
    /// <param name="id">ID of event to update, -1 to create</param>
    public static async Task<IActionResult> Update(
      HttpRequest req, ClaimsPrincipal principal, ILogger log, CloudBlobDirectory blobDirectory, int id)
    {
      var body = await req.GetBodyParameters();
      var deleteShifts = body["deleteShifts"]?.Value<JArray>().Select(s => (int)s);
      var shifts = body["shifts"]?.Values<JObject>();
      var eventCols = new List<string> { "name", "description", "transport", "address", "add_info" };

      // handle hours letter
      var letter = req.Form.Files.GetFile("letter");
      if (letter != null)
      {
        var blob = blobDirectory.GetBlockBlobReference(letter.FileName.WithTimestamp());
        await blob.UploadFromStreamAsync(letter.OpenReadStream());
        eventCols.Add("letter");
        body.Add("letter", blob.Uri.ToString());
      }

      object newId = null;
      var (eventQuery, eventParams) = FancyConn.MakeUpsertQuery("event", "event_id", id, eventCols, body);
      if (!string.IsNullOrEmpty(eventQuery))
      {
        var err = false;
        (err, newId) = await FancyConn.Shared.Scalar(eventQuery, eventParams);
        if (err) return Response.Error("Unable to update event.", FancyConn.Shared.lastError);
      }

      // handle deletion of shifts
      if (deleteShifts.Count() > 0)
      {
        var delQuery = $"DELETE FROM [shifts] WHERE [shift_id] IN ({string.Join(", ", deleteShifts.Map(x => $"@{x}"))})";
        var delParams = deleteShifts.ToDictionary(x => $"p{x}", x => (object)x);
        var (err, _) = await FancyConn.Shared.NonQuery(delQuery, delParams);
        if (err) return Response.Error("Unable to delete shifts.", FancyConn.Shared.lastError);
      }

      // handle shift updates
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

    /// <summary>
    /// Delete an event, including all shifts
    /// </summary>
    /// <param name="id">ID of event to delete</param>
    public static async Task<IActionResult> Delete(HttpRequest req, ClaimsPrincipal principal, ILogger log, int id)
    {
      var (err, _) = await FancyConn.Shared.NonQuery("DELETE FROM [event] WHERE [event_id] = @id",
        new Dictionary<string, object>() { { "id", id } });
      if (err) return Response.Error("Unable to delete event", FancyConn.Shared.lastError);

      return Response.Ok("Deleted event successfully.");
    }

    /// <summary>
    /// Set an event as archived or unarchived.
    /// GET /archive-event/10 to archive event #10
    /// GET /archive-event/10?unachive to unarchive event #10
    /// </summary>
    /// <param name="id">ID of event to (un)archive</param>
    public static async Task<IActionResult> Archive(HttpRequest req, ClaimsPrincipal principal, ILogger log, int id)
    {
      string unarchive = req.Query["unarchive"];
      if (unarchive == null)
      {
        var (err, _) = await FancyConn.Shared.NonQuery("UPDATE [event] SET [archived] = 1 WHERE [event_id] = @id",
          new Dictionary<string, object>() { { "id", id } });
        if (err) return Response.Error("Unable to archive event.", FancyConn.Shared.lastError);
        return Response.Ok("Archived event successfully.");
      }
      else
      {
        var (err, _) = await FancyConn.Shared.NonQuery("UPDATE [event] SET [archived] = 0 WHERE [event_id] = @id",
          new Dictionary<string, object>() { { "id", id } });
        if (err) return Response.Error("Unable to unarchive event.", FancyConn.Shared.lastError);
        return Response.Ok("Unarchived event successfully.");
      }
    }
  }
}
