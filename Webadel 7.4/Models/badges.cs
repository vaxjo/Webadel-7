using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Webadel7.DB_Badges;
using System.Web.UI;
using Newtonsoft.Json.Schema;

namespace Webadel7 {
    public class Badge {
        public enum AssignmentTypes { Manual = 0, Automatic = 1 }

        public int Id;
        public string Name, BadgeText, Description;
        public Guid CreatorId;
        public DateTime Added;
        public bool PendingApproval;
        public AssignmentTypes AssignmentType;

        public User Creator => User.Load(CreatorId);

        public Color Color {
            get {
                Random rnd = new Random(Id);
                return Color.FromArgb(rnd.Next(99), rnd.Next(99), rnd.Next(99));
            }
        }

        public string BackgroundColor => $"#{Color.R:D2}{Color.G:D2}{Color.B:D2}";

        public string BorderColor => $"#{Color.R * 7 / 10:D2}{Color.G * 7 / 10:D2}{Color.B * 7 / 10:D2}";

        internal Badge(DB_Badges.Badge dbItem) {
            Id = dbItem.id;
            Name = dbItem.name;
            BadgeText = dbItem.text;
            Description = dbItem.description;
            CreatorId = dbItem.creator;
            Added = dbItem.added;
            PendingApproval = dbItem.pendingApproval;
            AssignmentType = (AssignmentTypes)dbItem.assignmentTypes;
        }

        public void Save() {
            DB_Badges.DataContext dc = DB_Badges.DataContext.GetProfiledDC();

            var b = dc.Badges.Single(o => o.id == Id);

            b.name = Name;
            b.text = BadgeText;
            b.description = Description;
            b.pendingApproval = PendingApproval;
            b.assignmentTypes = (byte)AssignmentType;

            dc.SubmitChanges();

            //ClearCache();
        }

        public void Delete() => Remove(Id);

        public static List<Badge> GetAll() {
            return (List<Badge>)Myriads.Cache.Get("Badges", "all", delegate () {
                DB_Badges.DataContext dc = DB_Badges.DataContext.GetProfiledDC();
                return dc.Badges.Select(o => new Badge(o)).ToList();
            });
        }

        public static Badge Load(int id) => GetAll().FirstOrDefault(o => o.Id == id);

        public static bool Approve(int id) {
            DB_Badges.DataContext dc = DB_Badges.DataContext.GetProfiledDC();

            var b = dc.Badges.Single(o => o.id == id);
            if (b == null) return false;

            b.pendingApproval = false;
            dc.SubmitChanges();

            ClearCache();

            return true;
        }

        public static bool Remove(int id) {
            DB_Badges.DataContext dc = DB_Badges.DataContext.GetProfiledDC();

            var b = dc.Badges.Single(o => o.id == id);
            if (b == null) return false;

            dc.Badges.DeleteOnSubmit(b);
            dc.SubmitChanges();

            ClearCache();

            return true;
        }

        public static bool Submit(string name, string badgeText, string description, Guid creatorId) {
            User creator = User.Load(creatorId);

            Badge b = Create(name, badgeText, description, creatorId);
            if (b == null) return false;

            Room.PostToAide($"{creator.Username} has submitted a new badge: \"{name}\".");

            Award(27, creatorId); // "badge creator"
            return true;
        }

        public static Badge Create(string name, string badgeText, string description, Guid creatorId) {
            DB_Badges.DataContext dc = DB_Badges.DataContext.GetProfiledDC();

            if (dc.Badges.Any(o => o.name == name)) return null; // already exists

            var newBadge = new DB_Badges.Badge { name = name, text = badgeText, description = description, creator = creatorId, added = DateTime.Now, pendingApproval = true, assignmentTypes = 0 };
            dc.Badges.InsertOnSubmit(newBadge);
            dc.SubmitChanges();

            ClearCache();

            return Badge.Load(newBadge.id);
        }

        public static void ClearCache() => Myriads.Cache.Remove("Badges");

        public static bool Award(int badgeId, Guid recipientId) {
            DataContext dc = DataContext.GetProfiledDC();
            User recipient = User.Load(recipientId);
            Badge badge = Badge.Load(badgeId);

            if (dc.Badge_Users.Any(o => o.badgeId == badgeId && o.userId == recipientId)) return false; // already exists

            dc.Badge_Users.InsertOnSubmit(new DB_Badges.Badge_User { userId = recipientId, badgeId = badgeId, awarded = DateTime.Now, @new = true });
            dc.SubmitChanges();

            Room.PostToAide($"The '{badge.Name}' badge has been awarded to '{recipient.Username}'.");

            return true;
        }

        public static bool Unaward(int badgeId, Guid userId) {
            DataContext dc = DataContext.GetProfiledDC();

            var b = dc.Badge_Users.SingleOrDefault(o => o.badgeId == badgeId && o.userId == userId);
            if (b == null) return false; // can't find

            dc.Badge_Users.DeleteOnSubmit(b);
            dc.SubmitChanges();

            User user = User.Load(userId);
            Badge badge = Badge.Load(badgeId);

            Room.PostToAide($"The '{badge.Name}' badge has been removed from '{user.Username}'.");

            return true;
        }

        /// <summary> Get badges for specified user. If onlyNew is null then return all badges; if true only new badges, if false only not-new badges. </summary>
        public static List<Badge> GetBadges(Guid userId, bool? onlyNew = null) {
            DB_Badges.DataContext dc = DB_Badges.DataContext.GetProfiledDC();

            var badges = dc.Badge_Users.Where(o => o.userId == userId);

            if (onlyNew.HasValue) badges = badges.Where(o => o.@new == onlyNew.Value);

            return badges.OrderByDescending(o => o.awarded).Select(o => Badge.Load(o.badgeId)).ToList();
        }

        public static List<User> GetUsers(int badgeId) {
            DB_Badges.DataContext dc = DB_Badges.DataContext.GetProfiledDC();

            return dc.Badge_Users.Where(o => o.badgeId == badgeId && !o.User.disabled).OrderByDescending(o => o.awarded).Select(o => User.Load(o.userId)).ToList().Where(o => o.IsActiveUser).ToList();
        }

        /// <summary> Some badges are awarded automtically depending on timing. This method will execute whenever the system starts (~couple times a day). </summary>
        public static void AutoAward() {
            DB_Badges.DataContext dc = DB_Badges.DataContext.GetProfiledDC();

            // 1-year accont badge (#2)
            var oneYearAgo = new DateTime(DateTime.Now.Year - 1, DateTime.Now.Month, DateTime.Now.Day);
            foreach (var user in dc.Users.Where(o => o.created < oneYearAgo && o.lastActivity > SystemConfig.ActiveUserCutoff && !o.Badge_Users.Select(b => b.badgeId).Contains(2)).ToList()) Award(2, user.id);

            // 5-year accont badge (#3)
            var fiveYearsAgo = new DateTime(DateTime.Now.Year - 5, DateTime.Now.Month, DateTime.Now.Day);
            foreach (var user in dc.Users.Where(o => o.created < fiveYearsAgo && o.lastActivity > SystemConfig.ActiveUserCutoff && !o.Badge_Users.Select(b => b.badgeId).Contains(3)).ToList()) Award(3, user.id);

            // 10-year accont badge (#4)
            var tenYearsAgo = new DateTime(DateTime.Now.Year - 10, DateTime.Now.Month, DateTime.Now.Day);
            foreach (var user in dc.Users.Where(o => o.created < tenYearsAgo && o.lastActivity > SystemConfig.ActiveUserCutoff && !o.Badge_Users.Select(b => b.badgeId).Contains(4)).ToList()) Award(4, user.id);

            // 15-year accont badge (#7)
            var fifteenYearsAgo = new DateTime(DateTime.Now.Year - 15, DateTime.Now.Month, DateTime.Now.Day);
            foreach (var user in dc.Users.Where(o => o.created < fifteenYearsAgo && o.lastActivity > SystemConfig.ActiveUserCutoff && !o.Badge_Users.Select(b => b.badgeId).Contains(7)).ToList()) Award(7, user.id);

            DB.WebadelDataContext webDC = DB.WebadelDataContext.GetProfiledDC();

            // Divisive Poster (#12)
            List<DB.MessageVote> divisivePosts = webDC.MessageVotes.Where(o => o.Divisiveness >= 3).ToList();
            IEnumerable<Guid> divisiveAuthors = divisivePosts.Select(o => o.authorId).Distinct();
            foreach (Guid userId in divisiveAuthors) Award(12, userId);

            // very divisive posted (#22)
            foreach (Guid authorId in divisivePosts.Select(o => o.authorId).Distinct()) {
                if (divisivePosts.Count(o => o.authorId == authorId) > 1) Award(22, authorId);
            }

            // unpopular opinion (#11)
            foreach (Guid userId in webDC.MessageVotes.Where(o => o.Score <= -3).Select(o => o.authorId).Distinct()) Award(11, userId);

            // popular post (#10)
            foreach (Guid userId in webDC.MessageVotes.Where(o => o.Score >= 7).Select(o => o.authorId).Distinct()) Award(10, userId);

            // very popular post (#17)
            foreach (Guid userId in webDC.MessageVotes.Where(o => o.Score >= 12).Select(o => o.authorId).Distinct()) Award(17, userId);

            // plonked (#9)
            foreach (Guid userId in webDC.Plonks.Select(o => o.plonkedUserId).Distinct()) Award(9, userId);

            // 10-badges (#35)
            foreach (var user in dc.Users.Where(o => o.created < oneYearAgo && !o.Badge_Users.Select(b => b.badgeId).Contains(35)).ToList()) {
                if (user.Badge_Users.Count() >= 10) Award(35, user.id);
            }

            // 12-badges (#40)
            foreach (var user in dc.Users.Where(o => o.created < oneYearAgo && !o.Badge_Users.Select(b => b.badgeId).Contains(40)).ToList()) {
                if (user.Badge_Users.Count() >= 11) Award(40, user.id);
            }

            // no badges (61) - users with no badges get this one; users with badges do not
            foreach (User user in User.GetAll(true)) {
                var badges = GetBadges(user.Id);
                if (!badges.Any()) Award(61, user.Id); // if no badges, then award 61
                if (badges.Count > 1) Unaward(61, user.Id); // if more than 1 badge, remove 61
            }
        }

        /// <summary> Fires after a message has been posted. I imagine a number of badges might occur here. </summary>
        public static void AwardBadges(Message message) {
            DB.WebadelDataContext webDC = DB.WebadelDataContext.GetProfiledDC();

            // posted at 4:33 
            if (message.Date.Hour == 4 && message.Date.Minute == 33) Award(23, message.AuthorId);
            if (message.Date.Hour == 16 && message.Date.Minute == 33) Award(24, message.AuthorId);

            // 21:37 - pope death
            if (message.Date.Hour == 21 && message.Date.Minute == 37) Award(33, message.AuthorId);

            // 433 length
            if (message.Body.Length == 433) Award(28, message.AuthorId);

            // novelist / multi-page
            if (message.Body.Length > 3000) Award(26, message.AuthorId);

            // photographer (#29) more than 10 photos in last 72 hours
            int resourceCount = webDC.Resources.Count(o => o.uploaded > DateTime.Now.AddHours(-72) && o.Message.authorId == message.AuthorId);
            if (resourceCount > 10) Award(29, message.AuthorId);
        }
    }
}
