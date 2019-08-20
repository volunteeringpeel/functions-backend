using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using VP_Functions.Models;

namespace VP_Functions.API
{
  public enum Role : int
  {
    Volunteer = 1,
    Organizer = 2,
    Executive = 3
  }

  public static class API
  {
  }
}
