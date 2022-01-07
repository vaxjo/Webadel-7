using System.Web.Mvc;
using System.Web.Routing;

namespace Webadel7 {
    public class RouteConfig {
		public static void RegisterRoutes(RouteCollection routes) {
			routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

			// replaced with actual favicons
			// routes.MapRoute("Favicon", "favicon.ico", new { controller = "Res", action = "Favicon" });

			routes.MapRoute("Resource", "Res/{filename}", new { controller = "Res", action = "Index" });

			routes.MapRoute("Default", "{controller}/{action}/{id}", new { controller = "Room", action = "Index", id = UrlParameter.Optional });
		}
	}
}