using System;
using System.Web;
using System.Web.Mvc;

namespace Webadel7 {
	public class WebadelAuthorize : AuthorizeAttribute {
		protected override bool AuthorizeCore(HttpContextBase baseHttpContext) {
			return (MvcApplication.CurrentUser != null);
		}

		public static User GetUserFromCookie(HttpContext httpContext) {
			HttpCookie authCookie = httpContext.Request.Cookies[MvcApplication.AuthToken_CookieName];
			if (authCookie == null || string.IsNullOrWhiteSpace(authCookie.Value)) return null;

			// if (!GetAuthTokenId(authToken, httpContext.ApplicationInstance.Context).HasValue) return false;
			AuthToken authToken = AuthToken.Get(new Guid(authCookie.Value));
			if (authToken == null) return null;

			return Webadel7.User.Load(authToken.UserId);
		}

		/// <summary> Registers a failed login attempt and returns the total number of failed login attempts for this client. </summary>
		public static int AddFailedLoginAttempt() {
			int failedAttempts = (int)Myriads.Cache.Get("Failed Login Attempts", System.Web.HttpContext.Current.Request.UserHostAddress, delegate() { return 0; });
			failedAttempts++;

			Myriads.Cache.Add("Failed Login Attempts", System.Web.HttpContext.Current.Request.UserHostAddress, failedAttempts);
			return failedAttempts;
		}

		/// <summary> Resets the failed login attempt counter for this client (usually after a successful login). </summary>
		public static void ClearFailedLoginAttempt() {
			Myriads.Cache.Remove("Failed Login Attempts", System.Web.HttpContext.Current.Request.UserHostAddress);
		}
	}
}
