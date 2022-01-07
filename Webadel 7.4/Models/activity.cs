using System;
using System.Collections.Generic;
using System.Linq;

namespace Webadel7 {
    public abstract class SystemActivity {
        public class Activity_Item {
            public Guid UserId;
            public DateTime LastActivity;

            public string Username => User.Load(UserId).Username;
            public TimeSpan Idle => MvcApplication.Now.Subtract(LastActivity);
            public string Idle_Display => Idle.TotalMinutes.ToString("F0") + ":" + Idle.Seconds.ToString("D2");

            public override string ToString() => Username + " (" + Idle + ")";
        }

        public static List<Activity_Item> ActivityItems;

        static SystemActivity() {
            ActivityItems = new List<Activity_Item>();
        }

        public static void Update(Guid userId) {
            if (ActivityItems.Select(o => o.UserId).Contains(userId)) {
                ActivityItems.First(o => o.UserId == userId).LastActivity = MvcApplication.Now;
            } else {
                ActivityItems.Add(new Activity_Item { LastActivity = MvcApplication.Now, UserId = userId });
            }

            // also update the user record [jj 19Apr20]
            User.Load(userId).LastActivity = MvcApplication.Now;
        }

        public static void Remove(Guid userId) {
            ActivityItems.RemoveAll(o => o.UserId == userId);
        }

        public static void Save() {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            // added ".ToList()" to make a local copy to prevent "Collection was modified; enumeration operation may not execute" errors [jj 13Oct24]
            foreach (Activity_Item ai in ActivityItems.ToList()) {
                DB.User dbUser = dc.Users.Single(o => o.id == ai.UserId);
                dbUser.lastActivity = ai.LastActivity;
            }
            dc.SubmitChanges();

            // cull old activity items
            ActivityItems.RemoveAll(o => o.Idle.TotalMinutes > 15);
        }

        public static double ActivityIndex {
            get {
                return (double)Myriads.Cache.Get("Webadel", "ActivityIndex", delegate () {
                    DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
                    IEnumerable<double> last100ages = dc.Messages.OrderByDescending(o => o.date).Take(100).Select(o => o.date).ToList().Select(o => MvcApplication.Now.Subtract(o).TotalMinutes);
                    IEnumerable<double> inverses = last100ages.Select(o => 1 / o);
                    double sumOfInverse = inverses.Sum(); // older messages are "worth" less; a very recently posted message is worth a lot
                    return Math.Sqrt(100 * sumOfInverse); // sqrt() to get a better scale
                }, TimeSpan.FromMinutes(1)); // cache this value for a minute
            }
        }
    }
}
