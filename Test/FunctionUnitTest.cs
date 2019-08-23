using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using VP_Functions.API;
using Xunit.Abstractions;

namespace VP_Functions.Test
{
  public abstract class FunctionUnitTest
  {
    protected ClaimsPrincipal systemUser;
    protected ClaimsPrincipal volunteerUser;
    protected ILogger log = new ListLogger();
    protected ITestOutputHelper output;
    protected FancyConn conn;

    protected FunctionUnitTest(ITestOutputHelper output)
    {
      this.output = output;

      // set up mock environment variables
      using (StreamReader r = new StreamReader("local.settings.json"))
      {
        string json = r.ReadToEnd();
        dynamic settings = JsonConvert.DeserializeObject(json);
        foreach (JProperty kv in settings.Values)
        {
          Environment.SetEnvironmentVariable(kv.Name, kv.Value.ToString());
        }
      }
      this.conn = new FancyConn();

      // set up mock principal
      var claims = new List<Claim>()
      {
        new Claim(ClaimTypes.Email, "system@doesnotexist.volunteeringpeel.org"),
        new Claim(ClaimTypes.GivenName, "System"),
        new Claim(ClaimTypes.Surname, "Administrator")
      };
      var identity = new ClaimsIdentity(claims, "AuthenticationTypes.Federation");
      this.systemUser = new ClaimsPrincipal(identity);
      claims = new List<Claim>()
      {
        new Claim(ClaimTypes.Email, "test.volunteer@doesnotexist.volunteeringpeel.org"),
        new Claim(ClaimTypes.GivenName, "Volunteer"),
        new Claim(ClaimTypes.Surname, "System User")
      };
      this.volunteerUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "AuthenticationTypes.Federation"));
    }

    public DefaultHttpRequest CreateHttpRequest(ClaimsPrincipal auth = null, string queryStringKey = null, string queryStringValue = null)
    {
      if (auth == null) auth = this.systemUser;

      var context = new DefaultHttpContext();
      context.User = auth;
      var request = new DefaultHttpRequest(context);
      if (queryStringKey != null)
        request.Query = new QueryCollection(new Dictionary<string, StringValues>() {
          { queryStringKey, queryStringValue } });
      request.HttpContext.User = auth;
      return request;
    }
  }
}
