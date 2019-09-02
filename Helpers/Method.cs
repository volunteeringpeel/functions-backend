using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using VP_Functions.API;

namespace VP_Functions.Helpers
{
  public enum Role : int
  {
    None = 0,
    Volunteer = 1,
    Organizer = 2,
    Executive = 3
  }

  public enum ReqType : int
  {
    None = 0,
    ID = 1,
    New = 2,
    Current = 3 // Overrides Executive+ role requirement
  }

  public static class Method
  {
    public static async Task<IActionResult> IsUnauthenticated(
      Func<Task<IActionResult>> func, HttpRequest req, ILogger log) =>
      await IsUnauthenticated<object, object>((_1, _2, _3, _4) => func(), req, log, null, null);
    public static async Task<IActionResult> IsUnauthenticated(
      Func<HttpRequest, ILogger, Task<IActionResult>> func, HttpRequest req, ILogger log) =>
      await IsUnauthenticated<object, object>((r, l, _1, _2) => func(r, l), req, log, null, null);
    public static async Task<IActionResult> IsUnauthenticated<T1>(
      Func<HttpRequest, ILogger, T1, Task<IActionResult>> func, HttpRequest req, ILogger log, T1 param1) =>
      await IsUnauthenticated<T1, object>((r, l, p1, _) => func(r, l, p1), req, log, param1, null);
    public static async Task<IActionResult> IsUnauthenticated<T1, T2>(
      Func<HttpRequest, ILogger, T1, T2, Task<IActionResult>> func, HttpRequest req, ILogger log, T1 param1, T2 param2)
    {
      try
      {
        FancyConn.EnsureShared();
        return await func(req, log⁣, param1, param2);
      }
      catch (Exception e)
      {
        log.LogError("Unexpected error.", e);
        return Response.Error("Failed to process action.", e);
      }
      finally
      {
        FancyConn.Shared.Dispose();
      }
    }

    public static async Task<IActionResult> IsAuthenticated(
      Func<Task<IActionResult>> func, HttpRequest req, ClaimsPrincipal principal, ILogger log, bool authOverride = false) =>
      await IsAuthenticated<object, object>((_1, _2, _3, _4, _5) => func(), req, principal, log, null, null, authOverride);
    public static async Task<IActionResult> IsAuthenticated(
      Func<HttpRequest, ClaimsPrincipal, Task<IActionResult>> func,
      HttpRequest req, ClaimsPrincipal principal, ILogger log, bool authOverride = false) =>
      await IsAuthenticated<object, object>((r, p, _1, _2, _3) => func(r, p), req, principal, log, null, null, authOverride);
    public static async Task<IActionResult> IsAuthenticated(
      Func<HttpRequest, ClaimsPrincipal, ILogger, Task<IActionResult>> func,
      HttpRequest req, ClaimsPrincipal principal, ILogger log, bool authOverride = false) =>
      await IsAuthenticated<object, object>((r, p, l, _1, _2) => func(r, p, l), req, principal, log, null, null, authOverride);
    public static async Task<IActionResult> IsAuthenticated<T1>(
      Func<HttpRequest, ClaimsPrincipal, ILogger, T1, Task<IActionResult>> func,
      HttpRequest req, ClaimsPrincipal principal, ILogger log, T1 param1, bool authOverride = false) =>
      await IsAuthenticated<T1, object>((r, p, l, p1, _) => func(r, p, l, p1), req, principal, log, param1, null, authOverride);
    public static async Task<IActionResult> IsAuthenticated<T1, T2>(
      Func<HttpRequest, ClaimsPrincipal, ILogger, T1, T2, Task<IActionResult>> func,
      HttpRequest req, ClaimsPrincipal principal, ILogger log, T1 param1, T2 param2, bool authOverride = false)
    {
      try
      {
        string email = principal?.FindFirst(ClaimTypes.Email)?.Value;
        if (email == null)
          return Response.BadRequest("Not logged in.");

        FancyConn.EnsureShared();

        var role = await FancyConn.Shared.GetRole(email);
        // generate error if not an exec
        if (!authOverride && role < Role.Executive)
        {
          log.LogWarning($"Unauthorized access by {email} to {req.Path}");
          return Response.Error<object>($"Unauthorized.", statusCode: HttpStatusCode.Unauthorized);
        }

        return await func(req, principal, log, param1, param2);
      }
      catch (Exception e)
      {
        log.LogError("Unexpected error.", e);
        return Response.Error("Failed to process action.", e);
      }
      finally
      {
        FancyConn.Shared.Dispose();
      }
    }

    /// <summary>
    /// Special override for handling ReqType overrides of authentication system.
    /// User can edit own records, i.e. when ReqType is Current.
    /// </summary>
    public static async Task<IActionResult> IsAuthenticated<T1>(
      Func<HttpRequest, ClaimsPrincipal, ILogger, ReqType, T1, Task<IActionResult>> func,
      HttpRequest req, ClaimsPrincipal principal, ILogger log, ReqType reqType, T1 param1) =>
      await IsAuthenticated<T1, object>((r, p, l, p1, _) => func(r, p, l, reqType, p1), req, principal, log, param1, null, reqType == ReqType.Current);
  }
}
