using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using VP_Functions.Helpers;

namespace VP_Functions.API
{
  public static class Endpoints
  {
    // ===============
    // EVENT ENDPOINTS
    // ===============
    [FunctionName("GetAllEvents")]
    public static async Task<IActionResult> GetAllEvents(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "events")] HttpRequest req, ILogger log) =>
      await Method.IsUnauthenticated(Event.GetAll, req, log);
    [FunctionName("UpdateEvent")]
    public static async Task<IActionResult> UpdateEvent(
      [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "event/{id:int}")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log,
      [Blob("website-upload/hours-letters", FileAccess.ReadWrite)] CloudBlobDirectory blobDirectory, int id) =>
      await Method.IsAuthenticated(Event.Update, req, principal, log, blobDirectory, id);
    [FunctionName("DeleteEvent")]
    public static async Task<IActionResult> DeleteEvent(
      [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "event/{id:int}")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log, int id) =>
      await Method.IsAuthenticated(Event.Delete, req, principal, log, id);
    [FunctionName("ArchiveEvent")]
    public static async Task<IActionResult> ArchiveEvent(
      [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "archive-event/{id:int}")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log, int id) =>
      await Method.IsAuthenticated(Event.Archive, req, principal, log, id);

    // =============
    // FAQ ENDPOINTS
    // =============
    [FunctionName("GetAllFAQs")]
    public static async Task<IActionResult> GetAllFAQs(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "faq")] HttpRequest req, ILogger log) =>
      await Method.IsUnauthenticated(FAQ.GetAll, req, log);
    [FunctionName("CreateFAQ")]
    public static async Task<IActionResult> CreateFAQ(
      [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "faq")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log) =>
      await Method.IsAuthenticated(FAQ.CreateOrUpdate, req, principal, log, -1);
    [FunctionName("UpdateFAQ")]
    public static async Task<IActionResult> UpdateFAQ(
      [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "faq/{id:int}")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log, int id) =>
      await Method.IsAuthenticated(FAQ.CreateOrUpdate, req, principal, log, id);
    [FunctionName("DeleteFAQ")]
    public static async Task<IActionResult> DeleteFAQ(
      [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "faq/{id:int}")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log, int id) =>
      await Method.IsAuthenticated(FAQ.Delete, req, principal, log, id);

    // ================
    // HEADER ENDPOINTS
    // ================
    [FunctionName("CreateHeader")]
    public static async Task<IActionResult> CreateHeader(
      [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "header")] HttpRequest req,
      [Blob("website-upload/header-images", FileAccess.Write)] CloudBlobDirectory blobDirectory,
      ClaimsPrincipal principal, ILogger log) =>
      await Method.IsAuthenticated(Header.Create, req, principal, log, blobDirectory);
    [FunctionName("GetRandomHeader")]
    public static async Task<IActionResult> GetRandomHeader(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "header/random")] HttpRequest req, ILogger log) =>
      await Method.IsUnauthenticated(Header.GetRandom, req, log);
    [FunctionName("GetAllHeaders")]
    public static async Task<IActionResult> GetAllHeaders(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "header")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log) => await Method.IsAuthenticated(Header.GetAll, req, principal, log);
    [FunctionName("DeleteHeader")]
    public static async Task<IActionResult> DeleteHeader(
      [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "header/{id:int}")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log,
      [Blob("website-upload/header-images", FileAccess.Write)] CloudBlobDirectory blobDirectory, int id) =>
      await Method.IsAuthenticated(Header.Delete, req, principal, log, blobDirectory, id);


    // ===================
    // MAIL LIST ENDPOINTS
    // ===================
    [FunctionName("GetAllMailLists")]
    public static async Task<IActionResult> GetAllMailLists(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "mail-list")] HttpRequest req, ILogger log) =>
      await Method.IsUnauthenticated(MailList.GetAll, req, log);
    [FunctionName("CreateMailList")]
    public static async Task<IActionResult> CreateMailList(
      [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "mail-list")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log) =>
      await Method.IsAuthenticated(MailList.CreateOrUpdate, req, principal, log, -1);
    [FunctionName("UpdateMailList")]
    public static async Task<IActionResult> UpdateMailList(
      [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "mail-list/{id:int}")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log, int id) =>
      await Method.IsAuthenticated(MailList.CreateOrUpdate, req, principal, log, id);
    [FunctionName("DeleteMailList")]
    public static async Task<IActionResult> DeleteMailList(
      [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "mail-list/{id:int}")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log, int id) =>
      await Method.IsAuthenticated(MailList.Delete, req, principal, log, id);
    [FunctionName("MailListSignup")]
    public static async Task<IActionResult> MailListSignup(
      [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "mail-list/signup/{id:int}")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log, int id) =>
      await Method.IsAuthenticated(MailList.Signup, req, principal, log, id);

    // =================
    // SPONSOR ENDPOINTS
    // =================
    [FunctionName("GetAllSponsors")]
    public static async Task<IActionResult> GetAllSponsor(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sponsor")] HttpRequest req, ILogger log) =>
      await Method.IsUnauthenticated(FAQ.GetAll, req, log);
    [FunctionName("CreateSponsor")]
    public static async Task<IActionResult> CreateSponsor(
      [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sponsor")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log) =>
      await Method.IsAuthenticated(FAQ.CreateOrUpdate, req, principal, log, -1);
    [FunctionName("UpdateSponsor")]
    public static async Task<IActionResult> UpdateSponsor(
      [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "sponsor/{id:int}")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log, int id) =>
      await Method.IsAuthenticated(FAQ.CreateOrUpdate, req, principal, log, id);
    [FunctionName("DeleteSponsor")]
    public static async Task<IActionResult> DeleteSponsor(
      [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "sponsor/{id:int}")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log, int id) =>
      await Method.IsAuthenticated(FAQ.Delete, req, principal, log, id);

    // ==============
    // USER ENDPOINTS
    // ==============
    [FunctionName("CreateUser")]
    public static async Task<IActionResult> CreateUser(
      [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "user")] HttpRequest req,
      [Blob("website-upload/exec-photos", FileAccess.ReadWrite)] CloudBlobDirectory blobDirectory,
      ClaimsPrincipal principal, ILogger log)
      => await User.CreateOrUpdate(req, blobDirectory, principal, log, ReqType.New);
    [FunctionName("CreateOrUpdateUserByID")]
    public static async Task<IActionResult> CreateOrUpdateUserByID(
      [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "user/{id:int}")] HttpRequest req,
      [Blob("website-upload/exec-photos", FileAccess.ReadWrite)] CloudBlobDirectory blobDirectory,
      ClaimsPrincipal principal, ILogger log, int id)
      => await User.CreateOrUpdate(req, blobDirectory, principal, log, (id == -1) ? ReqType.New : ReqType.ID, id);
    [FunctionName("GetUserByID")]
    public static async Task<IActionResult> GetUserByID(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "user/{id:int}")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log, int id)
      => await Method.IsAuthenticated(User.Get, req, principal, log, ReqType.ID, id);
    [FunctionName("DeleteUser")]
    public static async Task<IActionResult> DeleteUser(
      [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "user/{id:int}")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log, int id)
      => await Method.IsAuthenticated(User.Delete, req, principal, log, id);
    // Current User
    [FunctionName("GetCurrentUser")]
    public static async Task<IActionResult> GetCurrentUser(
      [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log)
      => await Method.IsAuthenticated(User.Get, req, principal, log, ReqType.Current, -1);
    [FunctionName("UpdateCurrentUser")]
    public static async Task<IActionResult> UpdateCurrentUser(
      [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "me")] HttpRequest req,
      [Blob("website-upload/exec-photos", FileAccess.ReadWrite)] CloudBlobDirectory blobDirectory,
      ClaimsPrincipal principal, ILogger log)
      => await User.CreateOrUpdate(req, blobDirectory, principal, log, ReqType.Current);

    // =======================
    // MISCELLANEOUS ENDPOINTS
    // =======================
    [FunctionName("CreateSignup")]
    public static async Task<IActionResult> CreateSignup(
      [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "signup")] HttpRequest req,
      ClaimsPrincipal principal, ILogger log) => await Method.IsAuthenticated(Signup.Run, req, principal, log);
  }
}
