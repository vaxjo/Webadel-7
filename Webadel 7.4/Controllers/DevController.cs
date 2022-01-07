using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Net.Mail;

namespace Webadel7.Controllers {
    [WebadelAuthorize]
    public class DevController : WebadelController {
        protected override void OnActionExecuting(ActionExecutingContext filterContext) {
            base.OnActionExecuting(filterContext);

            // only aides and cosysops can use these
            if (!CurrentUser.Aide && !CurrentUser.CoSysop) filterContext.HttpContext.Response.Close();
        }

        public ActionResult GlyphTest () {
            return View();
        }

        public ActionResult DisplayPlonks () {
            return View(Webadel7.User.GetAll());
        }

        public ActionResult EmailTest () {
            return View();
        }

        public ActionResult SendEmail (string host,int port, string username, string password, string from, string to, string subject, string body) {

            SmtpClient smtp = new SmtpClient(host, port);
            smtp.Credentials = new System.Net.NetworkCredential(username, password);
            smtp.EnableSsl = true;
            smtp.Send(from, to, subject, body);

            return Redirect("/Dev/EmailTest");
        }

        public ActionResult DisplayAuthTokens () {
            //return View(AuthToken.GetAuthTokens(HttpContext.ApplicationInstance.Context));

            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            return View(dc.AuthTokens.ToList());
        }

        public ActionResult DisplayResources () {
            return View(Resource.GetAll());
        }

        public ActionResult UserLastActivity() {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            return View(dc.Users.ToList());
        }

        public ActionResult SimulateUser(Guid? userId) {
            if (!MvcApplication.CurrentUser.SysOp) return new HttpUnauthorizedResult();

            if (userId.HasValue) {
                // "logout" current
                SystemActivity.Remove(CurrentUser.Id);

                // remove auth token 
                HttpCookie authCookie = Request.Cookies[MvcApplication.AuthToken_CookieName];
                if (authCookie != null && !string.IsNullOrWhiteSpace(authCookie.Value)) AuthToken.Remove(new Guid(authCookie.Value));

                // "login"
                User user = Webadel7.User.Load(userId.Value);

                AuthToken authToken = AuthToken.New(user.Id);
                Response.Cookies[MvcApplication.AuthToken_CookieName].Value = authToken.Token.ToString();
                Response.Cookies[MvcApplication.AuthToken_CookieName].Expires = MvcApplication.Now.AddDays(9999);

                AuthToken.Add(authToken);
                //user.RecordIP(Request.UserHostAddress);
                //SystemActivity.Update(user.Id);
                //WebadelAuthorize.ClearFailedLoginAttempt();

                return Redirect("/");
            }

            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            return View(dc.Users.ToList());
        }

        public struct LastMessageRoom {
            public Room Room;
            public Guid? LastMessageId_Cached, LastMessageId_FromDB;
        }
        public ActionResult DisplayLastMessages () {
            ViewBag.CurrentUser = CurrentUser;

            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();

            return View(UserRoom.GetAll(CurrentUser.Id).Where(o => o.RoomId != SystemConfig.MailRoomId).Select(o => new LastMessageRoom() {
                Room = o.Room,
                LastMessageId_Cached = CurrentUser.GetMostRecentMessageId(o.RoomId),
                LastMessageId_FromDB = dc.Messages.OrderByDescending(m => m.date).FirstOrDefault(m => m.roomId == o.RoomId)?.id
            }).ToList());
        }

        public ActionResult SaveAuthTokens () {
            AuthToken.SaveAuthTokens(System.Web.HttpContext.Current);
            return Content("AuthToken.SaveAuthTokens(): finished");
        }

        public ActionResult CullEmptyRooms () {
            Room.CullEmptyRooms(System.Web.HttpContext.Current);
            return Content("Room.CullEmptyRooms(): finished");
        }

        public ActionResult CullOldMessages () {
            Message.CullOldMessages(System.Web.HttpContext.Current);
            return Content("Message.CullOldMessages(): finished");
        }

        public ActionResult CullOrphanedResources () {
            Resource.CullOrphanedResources(System.Web.HttpContext.Current);
            return Content("Resource.CullOrphanedResources(): finished");
        }

        public ActionResult ClearCache () {
            Myriads.Cache.Empty();
            return Content("ClearCache(): finished");
        }

        public ActionResult IPLookup() => View();

        public ActionResult IPLookup2(string ip) {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            ViewBag.IP = ip;
            return View(dc.User_IPs.Where(o => o.ip == ip).Select(o => o.User.username).Distinct().ToList());
        }
    }
}
