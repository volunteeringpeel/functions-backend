using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace VP_Functions.Models
{
  public class Response<T> where T : class
  {
    HttpStatusCode StatusCode;
    public T data;
    public string message;

    public Response(HttpStatusCode statusCode = HttpStatusCode.OK, string message = "", T data = null)
    {
      this.StatusCode = statusCode;
      this.message = message;
      this.data = data;
    }

    public HttpStatusCode GetStatusCode()
    {
      return this.StatusCode;
    }
  }

  // Helper class to avoid specifying <object> on everything
  public static class Respond
  {
    public static OkObjectResult Ok(string message) { return Ok<object>(message); }
    public static OkObjectResult Ok<T>(string message = "", T data = null,
      HttpStatusCode statusCode = HttpStatusCode.OK) where T : class
    {
      var res = new Response<T>(statusCode, message, data);
      var obj = new OkObjectResult(res);
      obj.StatusCode = (int)statusCode;
      return obj;
    }

    public static ObjectResult Error(string message) { return Error<object>(message); }
    public static ObjectResult Error<T>(string message = "Internal server error.", T data = null,
      HttpStatusCode statusCode = HttpStatusCode.InternalServerError) where T : class
    {
      var res = new Response<T>(statusCode, message, data);
      var obj = new ObjectResult(res);
      obj.StatusCode = (int)statusCode;
      return obj;
    }

    public static NotFoundObjectResult NotFound(string message = "Not found.")
    {
      var res = new Response<object>(HttpStatusCode.NotFound, message, null);
      var obj = new NotFoundObjectResult(res);
      return obj;
    }

    public static BadRequestObjectResult BadRequest(string message) { return BadRequest<object>(message); }
    public static BadRequestObjectResult BadRequest<T>(string message = "Bad parameters.", T data = null,
      HttpStatusCode statusCode = HttpStatusCode.BadRequest) where T : class
    {
      var res = new Response<T>(statusCode, message, data);
      var obj = new BadRequestObjectResult(res);
      obj.StatusCode = (int)statusCode;
      return obj;
    }
  }
}
