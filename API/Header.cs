using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
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
  class Header
  {
    /// <summary>
    /// Upload a header image
    /// </summary>
    [FunctionName("CreateHeader")]
    public static async Task<IActionResult> CreateHeader(
      [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "header")] HttpRequest req,
      [Blob("website-upload/header-images", FileAccess.Write)] CloudBlobDirectory blobDirectory,
      ClaimsPrincipal principal, ILogger log)
    {
      string email = principal?.FindFirst(ClaimTypes.Email)?.Value;
      if (email == null)
        return Response.BadRequest("Not logged in.");

      FancyConn.EnsureShared();

      try
      {
        var role = await FancyConn.Shared.GetRole(email);
        // generate error if not an exec AND not updating self
        if (role < Role.Executive)
        {
          log.LogWarning($"[header] Unauthorized attempt by {email} to upload an image");
          return Response.Error<object>($"Unauthorized.", statusCode: HttpStatusCode.Unauthorized);
        }
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
      catch (Exception e)
      {
        return Response.Error("Failed to create header image.", e);
      }
      finally
      {
        FancyConn.Shared.Dispose();
      }
    }

    /// <summary>
    /// Get a random header image
    /// </summary>
    [FunctionName("GetRandomHeader")]
    public static async Task<IActionResult> GetRandomHeader(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "header/random")] HttpRequest req, ILogger log)
    {
      FancyConn.EnsureShared();

      try
      {
        var (err, url) = await FancyConn.Shared.Scalar("SELECT TOP 1 [link] FROM [header] ORDER BY NEWID();");
        if (err) throw FancyConn.Shared.lastError; // bubble this to the catch block
        return new RedirectResult((string)url, false);
      }
      catch (Exception e)
      {
        log.LogError("Header image failed.", e);
        return new EmptyResult(); // can't do the normal return because we're expecting an image here
      }
      finally
      {
        FancyConn.Shared.Dispose();
      }
    }

    /// <summary>
    /// Get a random header image
    /// </summary>
    [FunctionName("GetAllHeaders")]
    public static async Task<IActionResult> GetAllHeaders(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "header")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log)
    {
      string email = principal?.FindFirst(ClaimTypes.Email)?.Value;
      if (email == null)
        return Response.BadRequest("Not logged in.");
      FancyConn.EnsureShared();

      try
      {
        var role = await FancyConn.Shared.GetRole(email);
        // generate error if not an exec AND not updating self
        if (role < Role.Executive)
        {
          log.LogWarning($"[header] Unauthorized attempt by {email} to get image list");
          return Response.Error<object>($"Unauthorized.", statusCode: HttpStatusCode.Unauthorized);
        }
        var (err, reader) = await FancyConn.Shared.Reader("SELECT [header_id], [link] FROM [header]");
        if (err) return Response.Error("Unable to get headers.", FancyConn.Shared.lastError);
        var headers = new JArray();
        var schema = reader.GetColumnSchema();
        while (reader.Read())
          headers.Add(new JObject(from c in schema
                                  select new JProperty(c.ColumnName, reader[(int)c.ColumnOrdinal])));

        return Response.Ok("Got headers successfully.", headers);
      }
      catch (Exception e)
      {
        return Response.Error("Failed to get headers", e);
      }
      finally
      {
        FancyConn.Shared.Dispose();
      }
    }

    /// <summary>
    /// Delete a header image
    /// </summary>
    [FunctionName("DeleteHeader")]
    public static async Task<IActionResult> DeleteHeader(
      [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "header/{id:int}")] HttpRequest req,
      [Blob("website-upload/header-images", FileAccess.Write)] CloudBlobDirectory blobDirectory,
      ClaimsPrincipal principal, ILogger log, int id)
    {
      string email = principal?.FindFirst(ClaimTypes.Email)?.Value;
      if (email == null)
        return Response.BadRequest("Not logged in.");

      FancyConn.EnsureShared();

      try
      {
        var role = await FancyConn.Shared.GetRole(email);
        // generate error if not an exec AND not updating self
        if (role < Role.Executive)
        {
          log.LogWarning($"[header] Unauthorized attempt by {email} to delete an image");
          return Response.Error<object>($"Unauthorized.", statusCode: HttpStatusCode.Unauthorized);
        }

        // delete file
        var (err, url) = await FancyConn.Shared.Scalar("SELECT [link] FROM [header] WHERE [header_id] = @id",
          new Dictionary<string, object>() { { "id", id } });
        var uri = new Uri((string)url);
        var fn = Path.GetFileName(uri.LocalPath);
        var blob = blobDirectory.GetBlockBlobReference(fn);
        await blob.DeleteIfExistsAsync();
        // delete record
        var link = blob.Uri.ToString();
        (err, _) = await FancyConn.Shared.NonQuery("DELETE FROM [header] WHERE [header_id] = @id;",
          new Dictionary<string, object>() { { "id", id } });

        return Response.Ok("Image deleted successfully.");
      }
      catch (Exception e)
      {
        return Response.Error("Failed to delete header image.", e);
      }
      finally
      {
        FancyConn.Shared.Dispose();
      }
    }
  }
}
