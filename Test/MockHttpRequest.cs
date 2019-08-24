using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Security.Claims;
using VP_Functions;

namespace VP_Functions.Test
{
  class MockHttpContext : DefaultHttpContext
  {
    public MockHttpContext WithRequestMethod(string method)
      => this.Then(t => t.Request.Method = method);

    public MockHttpContext WithBody(string body)
      => this.Then(t => t.Request.Body = GenerateStreamFromString(body));

    public MockHttpContext WithUser(ClaimsPrincipal user)
      => this.Then(t => t.User = user);

    protected Stream GenerateStreamFromString(string s) =>
      new MemoryStream()
        .Then(stream =>
        {
          var writer = new StreamWriter(stream);
          writer.Write(s);
          writer.Flush();
        })
        .Then(stream => stream.Position = 0);
  }
}
