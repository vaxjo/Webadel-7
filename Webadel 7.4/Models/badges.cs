﻿using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Webadel7 {
    public class Badge {
        public int Id;
        public string Name, BadgeText, Description;
        public Guid CreatorId;
        public DateTime Added;
        public bool PendingApproval;

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
        }

        public void Save() {
            DB_Badges.DataContext dc = DB_Badges.DataContext.GetProfiledDC();

            var b = dc.Badges.Single(o => o.id == Id);

            b.name = Name;
            b.text = BadgeText;
            b.description = Description;
            b.pendingApproval = PendingApproval;

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

        public static bool Submit(string name, string badgeText, string description, Guid creatorId) => Create(name, badgeText, description, creatorId) != null;

        public static Badge Create(string name, string badgeText, string description, Guid creatorId) {
            DB_Badges.DataContext dc = DB_Badges.DataContext.GetProfiledDC();

            if (dc.Badges.Any(o => o.name == name)) return null; // already exists

            var newBadge = new DB_Badges.Badge { name = name, text = badgeText, description = description, creator = creatorId, added = DateTime.Now, pendingApproval = true };
            dc.Badges.InsertOnSubmit(newBadge);
            dc.SubmitChanges();

            ClearCache();

            return Badge.Load(newBadge.id);
        }

        public static void ClearCache() => Myriads.Cache.Remove("Badges");

        public static bool Award(int badgeId, Guid recipientId) {
            DB_Badges.DataContext dc = DB_Badges.DataContext.GetProfiledDC();

            if (dc.Badge_Users.Any(o => o.badgeId == badgeId && o.userId == recipientId)) return false; // already exists

            dc.Badge_Users.InsertOnSubmit(new DB_Badges.Badge_User { userId = recipientId, badgeId = badgeId, awarded = DateTime.Now, @new = true });
            dc.SubmitChanges();

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

            return dc.Badge_Users.Where(o => o.badgeId == badgeId).OrderByDescending(o => o.awarded).Select(o => User.Load(o.userId)).ToList().Where(o => o.IsActiveUser).ToList();
        }

        /// <summary> Some badges are awarded automtically depending on timing. This method will execute several times a day. </summary>
        public static void AutoAward() {
            DB_Badges.DataContext dc = DB_Badges.DataContext.GetProfiledDC();

            // 1-year accont badge (#2)
            var oneYearAgo = new DateTime(DateTime.Now.Year - 1, DateTime.Now.Month, DateTime.Now.Day);
            foreach (var user in dc.Users.Where(o => o.created < oneYearAgo && !o.Badge_Users.Select(b => b.badgeId).Contains(2)).ToList()) Award(2, user.id);

            // 5-year accont badge (#3)
            var fiveYearsAgo = new DateTime(DateTime.Now.Year - 5, DateTime.Now.Month, DateTime.Now.Day);
            foreach (var user in dc.Users.Where(o => o.created < fiveYearsAgo && !o.Badge_Users.Select(b => b.badgeId).Contains(3)).ToList()) Award(3, user.id);

            // 10-year accont badge (#4)
            var tenYearsAgo = new DateTime(DateTime.Now.Year - 10, DateTime.Now.Month, DateTime.Now.Day);
            foreach (var user in dc.Users.Where(o => o.created < tenYearsAgo && !o.Badge_Users.Select(b => b.badgeId).Contains(4)).ToList()) Award(4, user.id);

            // 15-year accont badge (#7)
            var fifteenYearsAgo = new DateTime(DateTime.Now.Year - 15, DateTime.Now.Month, DateTime.Now.Day);
            foreach (var user in dc.Users.Where(o => o.created < fifteenYearsAgo && !o.Badge_Users.Select(b => b.badgeId).Contains(7)).ToList()) Award(7, user.id);
        }

        /// <summary> Remove all badge/user associations. </summary>
        public static bool RemoveAll() {
            DB_Badges.DataContext dc = DB_Badges.DataContext.GetProfiledDC();

            dc.Badge_Users.DeleteAllOnSubmit(dc.Badge_Users);
            dc.SubmitChanges();

            return true;
        }
    }
}
