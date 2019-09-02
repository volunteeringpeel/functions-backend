using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using VP_Functions.Helpers;

namespace VP_Functions.API
{
  public static class Header
  {
    /// <summary>
    /// Upload a header image.
    /// HTTP form body should include <see cref="File"/> pic containing the image to upload.
    /// </summary>
    public static async Task<IActionResult> Create(
      HttpRequest req, ClaimsPrincipal principal, ILogger log, CloudBlobDirectory blobDirectory)
    {
      var pic = req.Form.Files.GetFile("pic");
      if (pic == null) return Response.BadRequest("No image included in request.");

      var picStream = pic.OpenReadStream();
      var img = Image.Load(picStream);
      // resize to width of 1920 while maintaining aspect ratio
      if (img.Size().Width > 1920) img.Mutate(i => i.Resize(0, 1920));
      // upload into storage
      var blob = blobDirectory.GetBlockBlobReference(pic.FileName.WithTimestamp());
      await blob.UploadFromStreamAsync(pic.OpenReadStream());
      // insert record
      var link = blob.Uri.ToString();
      var (_, id) = await FancyConn.Shared.Scalar("INSERT INTO [header]([link]) VALUES (@link); SELECT SCOPE_IDENTITY();",
        new Dictionary<string, object>() { { "link", link } });

      return Response.Ok("Image uploaded successfully.", new { id, link });
    }

    /// <summary>
    /// Get a random header image.
    /// </summary>
    public static async Task<IActionResult> GetRandom()
    {
      var (err, url) = await FancyConn.Shared.Scalar("SELECT TOP 1 [link] FROM [header] ORDER BY NEWID();");
      if (err) throw FancyConn.Shared.lastError; // bubble this to the catch block
      return new RedirectResult((string)url, false);
    }

    /// <summary>
    /// Get all header images.
    /// </summary>
    public static async Task<IActionResult> GetAll()
    {
      var (err, reader) = await FancyConn.Shared.Reader("SELECT [header_id], [link] FROM [header]");
      if (err) return Response.Error("Unable to get headers.", FancyConn.Shared.lastError);
      var headers = new JArray();
      var schema = reader.GetColumnSchema();
      while (reader.Read())
        headers.Add(new JObject(from c in schema
                                select new JProperty(c.ColumnName, reader[(int)c.ColumnOrdinal])));

      return Response.Ok("Got headers successfully.", headers);
    }

    /// <summary>
    /// Delete a header image.
    /// </summary>
    public static async Task<IActionResult> Delete(
      HttpRequest req, ClaimsPrincipal principal, ILogger log, CloudBlobDirectory blobDirectory, int id)
    {
      // delete file
      var (err, url) = await FancyConn.Shared.Scalar("SELECT [link] FROM [header] WHERE [header_id] = @id",
        new Dictionary<string, object>() { { "id", id } });
      if (err) return Response.Error("Failed to find record.", FancyConn.Shared.lastError);
      var uri = new Uri((string)url);
      var fn = Path.GetFileName(uri.LocalPath);
      var blob = blobDirectory.GetBlockBlobReference(fn);
      await blob.DeleteIfExistsAsync();
      // delete record
      (err, _) = await FancyConn.Shared.NonQuery("DELETE FROM [header] WHERE [header_id] = @id;",
        new Dictionary<string, object>() { { "id", id } });
      if (err) return Response.Error("Failed to delete record.", FancyConn.Shared.lastError);

      return Response.Ok("Image deleted successfully.");
    }
  }
}
