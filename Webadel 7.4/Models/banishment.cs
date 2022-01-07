using System;
using System.Collections.Generic;
using System.Linq;

namespace Webadel7 {
    public class Banishment {
        public string IP;
        public DateTime? Expiration;

        internal Banishment(DB.Banishment dbBan) {
            IP = dbBan.ip;
            Expiration = dbBan.expiration;
        }
        
        /// <summary> Get all of the bans [cached]. </summary>
        public static List<Banishment> GetAll() {
            return (List<Banishment>)Myriads.Cache.Get("Banishments", "all", delegate () {
                DB.WebadelDataContext dc = new DB.WebadelDataContext();
                return dc.Banishments.Select(o => new Banishment(o)).ToList();
            });
        }

        public static void Update() {
            if (!GetAll().Any(o => o.Expiration.HasValue && o.Expiration.Value < MvcApplication.Now)) return;

            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            dc.Banishments.DeleteAllOnSubmit(dc.Banishments.Where(o => GetAll().Where(b => b.Expiration.HasValue && b.Expiration.Value < MvcApplication.Now).Select(b => b.IP).Contains(o.ip)));
            dc.SubmitChanges();

            Myriads.Cache.Remove("Banishments");
        }

        public static bool IsBanished(string ip) {
            Update();
            return GetAll().Any(o => o.IP == ip);
        }

        /// <summary> The time remaining until the specified host is no longer banned (return TimeSpan.Zero if not banned; TimeSpan.MaxValue if permanently banned). </summary>
        public static TimeSpan BanishedDuration(string ip) {
            Update();

            Banishment ban = GetAll().SingleOrDefault(o => o.IP == ip);
            if (ban == null) return TimeSpan.Zero;

            return (ban.Expiration.HasValue ? ban.Expiration.Value.Subtract(MvcApplication.Now) : TimeSpan.MaxValue);
        }

        /// <summary> Ban a client for specified amount of time (or perpetually if duration is null). </summary>
        public static void Ban(string ip, TimeSpan? duration = null) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            dc.Banishments.InsertOnSubmit(new DB.Banishment() { ip = ip, expiration = (duration.HasValue ? MvcApplication.Now.Add(duration.Value) : (DateTime?)null) });
            dc.SubmitChanges();

            Myriads.Cache.Remove("Banishments");
        }

        public static string NiceTimeSpanDisplay(TimeSpan ts) {
            if ((int)ts.TotalSeconds == 1) return "1 second";
            if (ts.TotalSeconds < 60) return ts.TotalSeconds.ToString("F0") + " seconds";
            if ((int)ts.TotalMinutes == 1) return "1 minute";
            if (ts.TotalMinutes < 60) return ts.TotalMinutes.ToString("F0") + " minutes";
            if ((int)ts.TotalHours == 1) return "1 hour";
            if (ts.TotalHours < 24) return ts.TotalHours.ToString("F0") + " hours";
            if ((int)ts.TotalDays == 1) return "1 day";
            return ts.TotalDays.ToString("F0") + " days";
        }
    }
}
