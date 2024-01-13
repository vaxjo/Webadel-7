using System;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using StackExchange.Profiling;

namespace Webadel7 {
    public class MvcApplication : System.Web.HttpApplication {
        public static readonly string AuthToken_CookieName;
        public static Random Rnd = new Random();

        /// <summary> Canonical Now. That is: Central time. </summary>
        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.Now.ToUniversalTime(), TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"));

        public static Webadel7.User CurrentUser {
            get {
                object cu = System.Web.HttpContext.Current.Items["Current User"];
                return (cu != null ? (Webadel7.User)cu : null);
            }
            set {
                System.Web.HttpContext.Current.Items.Add("Current User", value);
            }
        }

        public static string ClientTheme {
            get {
                HttpCookie themeCookie = System.Web.HttpContext.Current.Request.Cookies["webadelTheme"];
                if (themeCookie == null || string.IsNullOrWhiteSpace(themeCookie.Value)) return "default";
                return themeCookie.Value;
            }
        }

        public static bool IsWorkComputer {
            get {
                HttpCookie workCookie = System.Web.HttpContext.Current.Request.Cookies["isWorkComputer"];
                return (workCookie != null && workCookie.Value == "true");
            }
        }

        public static DateTime LastSystemChange {
            get {
                if (System.Web.HttpContext.Current.Application["lastSystemChange"] == null) System.Web.HttpContext.Current.Application["lastSystemChange"] = MvcApplication.Now;
                return (DateTime)System.Web.HttpContext.Current.Application["lastSystemChange"];
            }
            set {
                // context can be null if we're culling messages or rooms or something
                if (System.Web.HttpContext.Current != null) System.Web.HttpContext.Current.Application["lastSystemChange"] = value;
            }
        }

        // from http://stackoverflow.com/a/972189 [jj 13Oct24]
        // prevents PInvoke (not in NativeMethods class) or Stack walk (NativeMethods class) performance penalties.
        internal static partial class SafeNativeMethods {
            [System.Runtime.InteropServices.DllImport("kernel32")]
            internal extern static UInt64 GetTickCount64();
        }

        public static TimeSpan UpTime {
            get {
                return TimeSpan.FromMilliseconds(SafeNativeMethods.GetTickCount64());

                // from http://stackoverflow.com/a/972189 [jj 13Oct24]
                //using (var uptime = new System.Diagnostics.PerformanceCounter("System", "System Up Time")) {
                //    uptime.NextValue(); // Call this an extra time before reading its value
                //    return TimeSpan.FromSeconds(uptime.NextValue());
                //}
            }
        }

        public static string AssemblyVersion { get { return Assembly.GetAssembly(typeof(Webadel7.MvcApplication)).GetName().Version.ToString(); } }

        protected void Application_Start() {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            Room.PostToSystem(SystemConfig.SystemName + " running Webadel " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version + " started.");

            Badge.AutoAward();
        }

        protected void Application_BeginRequest() {
            if (Request.IsLocal) MiniProfiler.StartNew();

            Myriads.WebScheduler sched = new Myriads.WebScheduler(Context);
            sched.AddScheduledEvent("Save Users' Last Activity", delegate { SystemActivity.Save(); }, TimeSpan.FromMinutes(1));
            sched.AddScheduledEvent("Save Auth Tokens", delegate { AuthToken.SaveAuthTokens(Context); }, TimeSpan.FromSeconds(20), true);
            sched.AddScheduledEvent("Cull Old Messages", delegate { Message.CullOldMessages(Context); }, TimeSpan.FromHours(19));
            sched.AddScheduledEvent("Cull Empty Rooms", delegate { Room.CullEmptyRooms(Context); }, TimeSpan.FromHours(14));
            sched.AddScheduledEvent("Cull Orphaned Attachments", delegate { Resource.CullOrphanedResources(Context); }, TimeSpan.FromHours(15));

            MvcApplication.CurrentUser = WebadelAuthorize.GetUserFromCookie(Context);
        }

        protected void Application_EndRequest() {
            MiniProfiler.Current?.Stop();
        }

        static MvcApplication() {
            // generate a name for the authtoken cookie that is more-or-less unique to this site
            AuthToken_CookieName = System.Reflection.Assembly.GetExecutingAssembly().FullName.Split(' ')[0] + "_AuthToken";
        }
    }
}
