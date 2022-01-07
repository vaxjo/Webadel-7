using System;
using System.Configuration;

namespace Webadel7 {
    public class SystemConfig {
        public static string SystemName => ConfigurationManager.AppSettings["System Name"];
        public static Guid SysopId => new Guid(ConfigurationManager.AppSettings["Sysop"]);
        public static Guid AnonymousId => new Guid(ConfigurationManager.AppSettings["Anonymous"]);
        public static Guid LobbyRoomId => new Guid(ConfigurationManager.AppSettings["Lobby Room"]);
        public static Guid AideRoomId => new Guid(ConfigurationManager.AppSettings["Aide Room"]);
        public static Guid MailRoomId => new Guid(ConfigurationManager.AppSettings["Mail Room"]);
        public static int MessageCull => Convert.ToInt32(ConfigurationManager.AppSettings["Message Cull"]);
        public static Guid SystemRoomId => new Guid(ConfigurationManager.AppSettings["System Room"]);
        public static DateTime ActiveUserCutoff => MvcApplication.Now.AddDays(-Convert.ToInt32(ConfigurationManager.AppSettings["Active User Cutoff"]));
    }
}