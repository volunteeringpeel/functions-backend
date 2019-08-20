using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace VP_Functions.Models
{
  class Response
  {
    HttpStatusCode StatusCode;
    public object data;
    public string message;

    public Response(HttpStatusCode statusCode = HttpStatusCode.OK, string message = "", object data = null)
    {
      this.StatusCode = statusCode;
      this.message = message;
      this.data = data;
    }

    public static OkObjectResult Ok(string message = "", object data = null,
      HttpStatusCode statusCode = HttpStatusCode.OK)
    {
      var res = new Response(statusCode, message, data);
      var obj = new OkObjectResult(res);
      obj.StatusCode = (int)res.StatusCode;
      return obj;
    }

    public static ObjectResult Error(string message = "Internal server error.", object data = null,
      HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
    {
      var res = new Response(statusCode, message, data);
      var obj = new ObjectResult(res);
      obj.StatusCode = (int)res.StatusCode;
      return obj;
    }

    public static NotFoundObjectResult NotFound(string message = "Not found.")
    {
      var res = new Response(HttpStatusCode.NotFound, message, null);
      var obj = new NotFoundObjectResult(res);
      return obj;
    }

    public static BadRequestObjectResult BadRequest(string message = "Bad parameters.", object data = null,
      HttpStatusCode statusCode = HttpStatusCode.BadRequest)
    {
      var res = new Response(statusCode, message, data);
      var obj = new BadRequestObjectResult(res);
      obj.StatusCode = (int)res.StatusCode;
      return obj;
    }
  }
}
