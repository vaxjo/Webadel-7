using System;
using System.Net.Mail;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Webadel7.Controllers {
    public class AuthController : WebadelController {
        public struct Index_Message { public string Type, Message; }

        public ActionResult Index(string password) {
            List<Index_Message> messages = new List<Index_Message>();

            if (!string.IsNullOrWhiteSpace(password) && !Banishment.IsBanished(Request.UserHostAddress)) {
                User user = Webadel7.User.Load(password);
                if (user != null) return LoginUser(user);

                int failedAttempts = WebadelAuthorize.AddFailedLoginAttempt();
                messages.Add(new Index_Message() { Message = "Invalid password, please try again.", Type = "danger" });
                if (failedAttempts > 5) {
                    Banishment.Ban(Request.UserHostAddress, TimeSpan.FromSeconds(Math.Pow(failedAttempts - 5, 2)));
                    Room.PostToAide("Client [" + Request.UserHostAddress + "] has accumulated " + failedAttempts + " failed login attempts.");
                }
                ViewBag.LoginError = true;
            }

            if (Request["lostsession"] != null) messages.Add(new Index_Message() { Message = "Session lost, please re-login.", Type = "warning" });
            if (Request["logout"] != null) messages.Add(new Index_Message() { Message = "You have been logged out.", Type = "success" });
            if (Request["invalidlogintoken"] != null) messages.Add(new Index_Message() { Message = "Invalid login token.", Type = "warning" });

            if (Banishment.IsBanished(Request.UserHostAddress)) {
                if (Banishment.BanishedDuration(Request.UserHostAddress) == TimeSpan.MaxValue) messages.Add(new Index_Message() { Message = "Your IP has been permanently banished. You must have been very naughty. <a href='mailto:sysop@edsroom.com' class='alert-link'>Appeal?</a>", Type = "danger" });
                else messages.Add(new Index_Message() { Message = "Your IP has been temporarily banished. Try again in " + Banishment.NiceTimeSpanDisplay(Banishment.BanishedDuration(Request.UserHostAddress)) + ". <a href='mailto:sysop@edsroom.com' class='alert-link'>Appeal?</a>", Type = "danger" });
            }

            return View(messages);
        }

        public ActionResult LoginUser(Webadel7.User user) {
            AuthToken authToken = AuthToken.New(user.Id);

            // the previous code will actually make multiple, duplicate cookies
            // - or maybe not. it's hard to say what firefox is doing with these cookies
            //	Response.Cookies.Add(new HttpCookie(MvcApplication.AuthToken_CookieName, authToken.Token.ToString()) { Expires = MvcApplication.Now.AddDays(9999) });
            Response.Cookies[MvcApplication.AuthToken_CookieName].Value = authToken.Token.ToString();
            Response.Cookies[MvcApplication.AuthToken_CookieName].Expires = MvcApplication.Now.AddDays(9999);

            AuthToken.Add(authToken);

            user.RecordIP(Request.UserHostAddress);
            SystemActivity.Update(user.Id);
            WebadelAuthorize.ClearFailedLoginAttempt();

            return Redirect("/");
        }

        public ActionResult LoginToken(string token) {
            var user = Webadel7.User.GetUserForToken(token);
            if (user == null) return Redirect("/Auth/Index?invalidlogintoken=true");

            Room.PostToAide(user.Username + " logged in with a one-time login token.");

            return LoginUser(user);
        }

        public Myriads.JsonNetResult NewUser(string username, string newPassword, string confirmPassword, string email) {
            if (!Webadel7.User.CanCreateAccount(Request.UserHostAddress)) throw new Exception("User cannot create account.");

            if (username == null) return JsonNet("usernameempty"); // if this happens, an invalid request was made outside of the modal context (perhaps a crawler?)

            List<string> errors = new List<string>();

            if (Banishment.IsBanished(Request.UserHostAddress)) errors.Add("banished");
            if (string.IsNullOrWhiteSpace(username)) errors.Add("usernameempty");
            if (string.IsNullOrWhiteSpace(newPassword)) errors.Add("passwordempty");

            if (newPassword != confirmPassword) errors.Add("confirm");
            if (username.Trim() == newPassword.Trim()) errors.Add("pwisusername");

            if (Webadel7.User.GetAll().Any(o => o.Username.ToLower() == username.ToLower())) errors.Add("usernameexists");

            User conflictUser = Webadel7.User.Load(newPassword);
            if (conflictUser != null) errors.Add("badpw");

            if (errors.Count > 0) return JsonNet(string.Join(",", errors));

            Webadel7.User newUser = Webadel7.User.NewUser(username, newPassword, email);
            newUser.RecordIP(Request.UserHostAddress);
            Room.PostToAide($"New user ({newUser}) created [<a href='https://who.is/whois-ip/ip-address/{Request.UserHostAddress}'>{Request.UserHostAddress}</a>].");

            Room mail = Room.Load(SystemConfig.MailRoomId);
            mail.Post(System.IO.File.ReadAllText(Server.MapPath("~/App_Data/newUserMail.htm")), SystemConfig.SysopId, newUser.Id);

            return JsonNet(""); // return empty on success
        }

        public Myriads.CallbackResult CreateLoginToken(string email) {
            if (string.IsNullOrWhiteSpace(email)) return Myriads.CallbackResult.Get(100, "You've gotta enter an email address.");

            User user = Webadel7.User.Get(email);
            if (user == null) return Myriads.CallbackResult.Get(200, "Email not found.");

            string loginToken = user.CreateLoginToken();

            // send email
            try {
                Dictionary<string, string> replacements = new Dictionary<string, string>();
                replacements.Add("systemName", SystemConfig.SystemName);
                replacements.Add("Request.Url.Scheme", Request.Url.Scheme);
                replacements.Add("Request.Url.Authority", Request.Url.Authority);
                replacements.Add("loginToken", loginToken);

                MailMessage mail = Myriads.MailTemplate.CreateMailMessage(Server.MapPath("~/App_Data/Email/logintoken.xml"));
                mail.To.Add(email);
                Myriads.MailTemplate.MakeReplacements(mail, replacements);

                SmtpClient smtp = new SmtpClient();
                smtp.Send(mail);

            } catch (Exception e) {
                Room.PostToSystem("Error trying to send email to [" + email + "]: " + e.Message);
                return Myriads.CallbackResult.Get(300, "Error sending email.");
            }

            return Myriads.CallbackResult.Get(0, "An email containing a one-time login link has been dispatched.");
        }

        // this is polled frequently
        public Myriads.JsonNetResult GetUpdate(Guid? roomId) {
            if (CurrentUser == null) return JsonNet(new { error = "nosession" });

            Dictionary<Guid, int> numNew = CurrentUser.GetNumNewMessages();

            List<Guid> forgottenRooms = UserRoom.GetAll(CurrentUser.Id).Where(o => o.Forgotten).Select(o => o.RoomId).ToList();
            int totalNew = numNew.Where(o => !forgottenRooms.Contains(o.Key)).Sum(o => o.Value);

            UserRoom.UnexpireForgotten();

            return JsonNet(new {
                error = "",
                newMessages = numNew,
                totalNew = totalNew,
                lastSystemChange = MvcApplication.LastSystemChange.Ticks,
                mostRecentMessage = (roomId.HasValue ? CurrentUser.GetMostRecentMessageId(roomId.Value) : (Guid?)null)
            });
        }

        public Myriads.JsonNetResult Online() {
            return JsonNet(new {
                index = SystemActivity.ActivityIndex.ToString("F2"),
                activity = SystemActivity.ActivityItems.OrderBy(o => o.Idle)
            });
        }

        public ActionResult Logout() {
            if (CurrentUser != null) SystemActivity.Remove(CurrentUser.Id);

            // remove auth token [jj 13Oct24]
            HttpCookie authCookie = Request.Cookies[MvcApplication.AuthToken_CookieName];
            if (authCookie != null && !string.IsNullOrWhiteSpace(authCookie.Value))
                AuthToken.Remove(new Guid(authCookie.Value));

            Response.Cookies.Add(new HttpCookie(MvcApplication.AuthToken_CookieName, "") { Expires = MvcApplication.Now.AddMinutes(-1) });
            return Redirect("/Auth/Index?logout=true");
        }
    }
}
