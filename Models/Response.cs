using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace VP_Functions.Models
{
  class Response
  {
    private int StatusCode;
    public Object data;
    public string message;

    public Response(int statusCode = 200, string message = "", Object data = null)
    {
      this.StatusCode = statusCode;
      this.message = message;
      this.data = data;
    }

    public static OkObjectResult Ok(int statusCode = 200, string message = "", Object data = null)
    {
      var res = new Response(statusCode, message, data);
      var obj = new OkObjectResult(res);
      obj.StatusCode = res.StatusCode;
      return obj;
    }

    public static NotFoundObjectResult NotFound(string message = "Not found.")
    {
      var res = new Response(404, message, null);
      var obj = new NotFoundObjectResult(res);
      return obj;
    }

    public static ObjectResult Error(int statusCode = 500, string message = "Internal server error.", Object data = null)
    {
      var res = new Response(statusCode, message, data);
      var obj = new ObjectResult(res);
      obj.StatusCode = res.StatusCode;
      return obj;
    }
  }
}
