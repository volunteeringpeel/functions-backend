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

    public OkObjectResult Result()
    {
      var res = new OkObjectResult(this);
      res.StatusCode = this.StatusCode;
      return res;
    }
  }
}
