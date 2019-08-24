using Microsoft.AspNetCore.Mvc;
using FluentAssertions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using VP_Functions.Models;
using Xunit;
using Xunit.Abstractions;
using Newtonsoft.Json.Linq;

namespace VP_Functions.Test
{
  public class UserUnitTests : FunctionUnitTest
  {
    public UserUnitTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public async void GetUser_should_be_authorized()
    {
      var req = new MockHttpContext().Request;
      var result = (ObjectResult)await API.User.GetUser(req, null, log, 0);
      output.WriteLine(JsonConvert.SerializeObject(result.Value));
      result.Value.As<Response>().message.Should().Be("Not logged in.");
      result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async void GetUser_should_be_restricted()
    {
      var req = new MockHttpContext()
        .WithUser(this.volunteerUser)
        .Request;
      var result = (ObjectResult)await API.User.GetUser(req, this.volunteerUser, log, 0);
      output.WriteLine(JsonConvert.SerializeObject(result.Value));
      result.Value.As<Response>().message.Should().Be("Unauthorized.");
      result.StatusCode.Should().Be(401);
    }

    [Fact]
    public async void GetUser_should_return_the_right_user()
    {
      var req = new MockHttpContext()
        .WithUser(this.systemUser)
        .Request;
      var result = (ObjectResult)await API.User.GetUser(req, this.systemUser, log, 1);
      var response = result.Value.As<Response>();
      output.WriteLine(JsonConvert.SerializeObject(response));
      response.data["email"].Value<string>().Should().Be("test.volunteer@doesnotexist.volunteeringpeel.org");
      response.message.Should().Be("Retrieved user successfully.");
      result.StatusCode.Should().Be(200);
    }

    [Fact]
    public async void GetCurrentUser_should_return_the_right_user()
    {
      var req = new MockHttpContext()
        .WithUser(this.systemUser)
        .Request;
      var result = (ObjectResult)await API.User.GetCurrentUser(req, this.systemUser, log);
      var response = result.Value.As<Response>();
      output.WriteLine(JsonConvert.SerializeObject(response));
      response.data["user"]["email"].Value<string>().Should().Be("system@doesnotexist.volunteeringpeel.org");
      response.message.Should().Be("Retrieved user successfully.");
      result.StatusCode.Should().Be(200);
    }

    [Fact]
    public async void SetUser_should_update()
    {
      var req = new MockHttpContext()
        .WithUser(this.systemUser)
        .WithBody(@"{
          ""phone_1"":""5555555555""
        }")
        .Request;
      var result = (ObjectResult)await API.User.SetUser(req, this.systemUser, log, 0);
      output.WriteLine(JsonConvert.SerializeObject(result.Value));
      result.Value.As<Response>().message.Should().Be("User updated successfully.");
    }

    [Fact]
    public async void SetUser_should_prevent_injection()
    {
      var req = new MockHttpContext()
        .WithUser(this.systemUser)
        .WithBody(@"{
          ""a_fake_column"":""haha_im_haxor""
        }")
        .Request;
      var result = (ObjectResult)await API.User.SetUser(req, this.systemUser, log, 0);
      output.WriteLine(JsonConvert.SerializeObject(result.Value));
      result.Value.As<Response>().message.Should().Be("Passed unsupported column a_fake_column.");
    }
  }
}
