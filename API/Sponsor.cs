using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using VP_Functions.Helpers;

namespace VP_Functions.API
{
  public static class Sponsor
  {
    /// <summary>
    /// Get all sponsors
    /// </summary>
    /// <returns>List of sponsors</returns>
    public static async Task<IActionResult> GetAll()
    {
      var (err, reader) = await FancyConn.Shared.Reader(@"SELECT
          [sponsor_id], [name], [image], [website], [priority]
        FROM [sponsor] ORDER BY [priority]");
      if (err) return Response.Error("Unable to get sponsors.", FancyConn.Shared.lastError);
      var faqs = reader.ToJArray();
      reader.Close();

      return Response.Ok("Got FAQs successfully.", faqs);
    }

    /// <summary>
    /// Update a sponsor by ID, create if not exists
    /// </summary>
    /// <param name="id">ID of sponsor to modify, use -1 to always create.</param>
    public static async Task<IActionResult> CreateOrUpdate(
      HttpRequest req, ClaimsPrincipal principal, ILogger log, CloudBlobDirectory blobDirectory, int id)
    {
      var body = await req.GetBodyParameters();
      var cols = new List<string>() { "name", "image", "website", "priority" };

      // handle image upload
      var pic = req.Form.Files.GetFile("pic");
      if (pic != null)
      {
        var picStream = pic.OpenReadStream();
        var img = Image.Load(picStream);
        // resize to width of 350px while maintaining aspect ratio
        img.Mutate(i => i.Resize(0, 350));

        var blob = blobDirectory.GetBlockBlobReference(pic.FileName.WithTimestamp());
        await blob.UploadFromStreamAsync(pic.OpenReadStream());
        cols.Add("pic");
        body.Add("pic", blob.Uri.ToString());
      }

      // handle database
      var (query, param) = FancyConn.MakeUpsertQuery("sponsor", "sponsor_id", id, cols, body);
      var (err, newId) = await FancyConn.Shared.Scalar(query, param);
      if (err) return Response.Error("Unable to edit sponsor record.", FancyConn.Shared.lastError);

      return Response.Ok($"{(newId == null ? "Updated" : "Created")} sponsor successfully.");
    }

    /// <summary>
    /// Delete a sponsor by ID
    /// </summary>
    /// <param name="id">ID of sponsor to delete</param>
    public static async Task<IActionResult> Delete(HttpRequest req, ClaimsPrincipal principal, ILogger log, int id)
    {
      var (err, rows) = await FancyConn.Shared.NonQuery("DELETE FROM [sponsor] WHERE [sponsor_id] = @id",
        new Dictionary<string, object>() { { "id", id } });
      if (err || rows != 1) return Response.Error("Unable to delete sponsor.", FancyConn.Shared.lastError);

      return Response.Ok("Deleted sponsor successfully.");
    }
  }
}
