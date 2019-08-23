using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using VP_Functions.Models;
using Xunit;
using Xunit.Abstractions;

namespace VP_Functions.Test
{
  public class UserUnitTests : FunctionUnitTest
  {
    public UserUnitTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async void GetUser_should_be_authorized()
    {
      var req = this.CreateHttpRequest();
      var result = (ObjectResult)await API.User.GetUser(req, null, log, 0, this.conn);
      output.WriteLine(JsonConvert.SerializeObject(result.Value));
      var response = (Response<object>)result.Value;
      Assert.Equal("Not logged in.", response.message);
      Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async void GetUser_should_be_restricted()
    {
      var req = this.CreateHttpRequest(this.volunteerUser);
      var result = (ObjectResult)await API.User.GetUser(req, this.volunteerUser, log, 0, this.conn);
      output.WriteLine(JsonConvert.SerializeObject(result.Value));
      var response = (Response<object>)result.Value;
      Assert.Equal("Unauthorized.", response.message);
      Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async void GetUser_should_return_the_right_user()
    {
      var req = this.CreateHttpRequest(this.systemUser);
      var result = (ObjectResult)await API.User.GetUser(req, this.systemUser, log, 1, this.conn);
      output.WriteLine(JsonConvert.SerializeObject(result.Value));
      var response = (Response<Dictionary<string, object>>)result.Value;
      Assert.Equal("test.volunteer@doesnotexist.volunteeringpeel.org", response.data["email"]);
      Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async void GetCurrentUser_should_return_the_right_user()
    {
      var req = this.CreateHttpRequest(this.systemUser);
      var result = (ObjectResult)await API.User.GetCurrentUser(req, this.systemUser, log, this.conn);
      var json = JsonConvert.SerializeObject(result.Value);
      var expected = "{\"data\":{\"user\":{\"user_id\":0,\"role_id\":3,\"first_name\":\"System\",\"last_name\":\"Administrator\",\"email\":\"system@doesnotexist.volunteeringpeel.org\",\"phone_1\":null,\"phone_2\":null,\"school\":null,\"title\":null,\"bio\":null,\"pic\":null,\"show_exec\":0},\"created\":false,\"userShifts\":[]},\"message\":\"Retrieved user successfully.\"}";
      output.WriteLine(JsonConvert.SerializeObject(result.Value));
      Assert.Equal(expected, json);
      Assert.Equal(200, result.StatusCode);
    }
  }
}
