using System;
using System.Collections.Generic;
using System.Linq;

namespace Webadel7 {
    public class UserProfile {
        public readonly Guid Id;
        public string Bio, Website, Email, Location, Pronouns;
        public DateTime? Birthdate; // set year to 1900 to indicate that we have only date and month
        public bool BirthdayHasYear;

        public bool IsBirthday => Birthdate.HasValue && Birthdate.Value.Month == DateTime.Now.Month && Birthdate.Value.Day == DateTime.Now.Day;

        public double? Age => Birthdate.HasValue ? Birthdate.Value.GetYears(DateTime.Now): (double?)null;

        public bool IsEmpty => string.IsNullOrWhiteSpace(Bio) && string.IsNullOrWhiteSpace(Website) && string.IsNullOrWhiteSpace(Email) && string.IsNullOrWhiteSpace(Location) && string.IsNullOrWhiteSpace(Pronouns);

        public UserProfile(Guid id) { Id = id; }
        internal UserProfile(DB.UserProfile dbUser) {
            Id = dbUser.id;
            Bio = dbUser.bio;
            Website = dbUser.website;
            Email = dbUser.email;
            Location = dbUser.location;
            Pronouns = dbUser.pronouns;
            Birthdate = dbUser.birthdate;
            BirthdayHasYear = (Birthdate.HasValue && Birthdate.Value.Year != 1900);
        }

        public void Save() {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            DB.UserProfile dbUserProfile = dc.UserProfiles.SingleOrDefault(o => o.id == Id);
            if (dbUserProfile == null) {
                dbUserProfile = new DB.UserProfile() { id = Id };
                dc.UserProfiles.InsertOnSubmit(dbUserProfile);
            }
            dbUserProfile.bio = Bio;
            dbUserProfile.website = Website;
            dbUserProfile.email = Email;
            dbUserProfile.location = Location;
            dbUserProfile.pronouns = Pronouns;
            dbUserProfile.birthdate = Birthdate;

            dc.SubmitChanges();

            Myriads.Cache.Remove("UserProfiles");
        }

        /// <summary> Get all of the profiles [cached]. </summary>
        public static List<UserProfile> GetAll() {
            return (List<UserProfile>)Myriads.Cache.Get("UserProfiles", "all", delegate () {
                DB.WebadelDataContext dc = new DB.WebadelDataContext();
                return dc.UserProfiles.Select(o => new UserProfile(o)).ToList();
            });
        }

        public static UserProfile Load(Guid id) {
            return GetAll().SingleOrDefault(o => o.Id == id) ?? new UserProfile(id);
        }
    }

    public static class Extensions {
        /// <summary> Number of years between two dates. </summary>
        public static double GetYears(this DateTime date, DateTime futureDate) {
            int years = futureDate.Year - date.Year;

            double diff = (double)futureDate.DayOfYear - (double)date.DayOfYear;
            return years + diff / 365.25;
        }
    }
}

