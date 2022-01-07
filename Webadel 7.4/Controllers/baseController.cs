using System;
using System.Web;

namespace Webadel7.Controllers {
	public class WebadelController : Myriads.JsonNetController {
		public User CurrentUser {
			get {
				// removing session [jj 13Dec14]
				//if (System.Web.HttpContext.Current.Session["userId"] == null || Banishment.IsBanished(Request.UserHostAddress)) return null;
				//return Webadel7.User.Load((Guid)System.Web.HttpContext.Current.Session["userId"]);

				//object userId = System.Web.HttpContext.Current.Items["userId"];
				//if (userId == null || Banishment.IsBanished(Request.UserHostAddress)) return null;
				//return Webadel7.User.Load((Guid)userId);
				
				//Guid? userId = WebadelAuthorize.GetUserIdFromCookie(new HttpContextWrapper(System.Web.HttpContext.Current));
				//return (userId.HasValue ? Webadel7.User.Load(userId.Value) : null);

				return MvcApplication.CurrentUser;
			}
		}
	}
}
