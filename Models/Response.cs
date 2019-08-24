using System.Net;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace VP_Functions.Models
{
  public class Response
  {
    HttpStatusCode StatusCode;
    public JToken data;
    public string message;

    public Response(HttpStatusCode statusCode = HttpStatusCode.OK, string message = "", object data = null)
    {
      this.StatusCode = statusCode;
      this.message = message;
      if (data is JObject)
      {
        this.data = data as JObject;
      }
      else if (data == null)
      {
        this.data = JValue.CreateNull();
      }
      else
      {
        this.data = JToken.FromObject(data);
      }
    }

    public HttpStatusCode GetStatusCode()
    {
      return this.StatusCode;
    }

    public static OkObjectResult Ok(string message) { return Ok<JToken>(message); }
    public static OkObjectResult Ok<T>(string message = "", T data = null,
      HttpStatusCode statusCode = HttpStatusCode.OK) where T : class
    {
      var res = new Response(statusCode, message, data);
      var obj = new OkObjectResult(res);
      obj.StatusCode = (int)statusCode;
      return obj;
    }

    public static ObjectResult Error(string message) { return Error<JToken>(message); }
    public static ObjectResult Error<T>(string message = "Internal server error.", T data = null,
      HttpStatusCode statusCode = HttpStatusCode.InternalServerError) where T : class
    {
      var res = new Response(statusCode, message, data);
      var obj = new ObjectResult(res);
      obj.StatusCode = (int)statusCode;
      return obj;
    }

    public static NotFoundObjectResult NotFound(string message = "Not found.")
    {
      var res = new Response(HttpStatusCode.NotFound, message, null);
      var obj = new NotFoundObjectResult(res);
      return obj;
    }

    public static BadRequestObjectResult BadRequest(string message) { return BadRequest<object>(message); }
    public static BadRequestObjectResult BadRequest<T>(string message = "Bad parameters.", T data = null,
      HttpStatusCode statusCode = HttpStatusCode.BadRequest) where T : class
    {
      var res = new Response(statusCode, message, data);
      var obj = new BadRequestObjectResult(res);
      obj.StatusCode = (int)statusCode;
      return obj;
    }
  }
}
