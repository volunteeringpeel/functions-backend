using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using VP_Functions.Helpers;
using System.Security.Claims;
using System.Collections.Generic;

namespace VP_Functions.API
{
  public static class FAQ
  {
    /// <summary>
    /// Get all FAQs
    /// </summary>
    /// <returns>List of FAQs</returns>
    public static async Task<IActionResult> GetAll()
    {
      var (err, reader) = await FancyConn.Shared.Reader("SELECT [faq_id], [question], [answer] FROM [faq]");
      if (err) return Response.Error("Unable to get FAQs.", FancyConn.Shared.lastError);
      var faqs = reader.ToJArray();

      return Response.Ok("Got FAQs successfully.", faqs);
    }

    /// <summary>
    /// Update an FAQ by ID, create if not exists
    /// </summary>
    /// <param name="id">ID of FAQ to modify, use -1 to always create.</param>
    public static async Task<IActionResult> CreateOrUpdate(HttpRequest req, ClaimsPrincipal principal, ILogger log, int id)
    {
      var body = await req.GetBodyParameters();
      var (query, param) = FancyConn.MakeUpsertQuery("faq", "faq_id", id, new List<string>() { "question", "answer" }, body);
      var (err, newId) = await FancyConn.Shared.Scalar(query, param);
      if (err) return Response.Error("Unable to edit FAQ record.", FancyConn.Shared.lastError);

      return Response.Ok($"{(newId == null ? "Updated" : "Created")} FAQ successfully.");
    }

    public static async Task<IActionResult> Delete(HttpRequest req, ClaimsPrincipal principal, ILogger log, int id)
    {
      var (err, rows) = await FancyConn.Shared.NonQuery("DELETE FROM [faq] WHERE [faq_id] = @id",
        new Dictionary<string, object>() { { "id", id } });
      if (err || rows != 1) return Response.Error("Unable to get FAQs.", FancyConn.Shared.lastError);

      return Response.Ok("Deleted FAQ successfully.");
    }
  }
}
