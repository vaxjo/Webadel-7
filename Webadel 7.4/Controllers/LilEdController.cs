using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;

namespace Webadel7.Controllers {
    [WebadelAuthorize]
    public class LilEdController : WebadelController {

        public ActionResult Found(string let) {
            int n = LittleEd.Found(let);

            if (n == 0) return Content("No you didn't.");

            if (n == 1) return Content("You found a little ed!");

            if (n < 5) return Content("You found another little ed!");

            string[] phrases = new string[] {
                "Another little ed found!",
                "Add another little ed to your collection",
                "You've found a lot of little eds!",
                "Those little eds aren't too sneaky for you!",
                "No little ed can hide from you!",
                "You keep find little eds!",
                "You know just where to find those little eds.",
                "Little eds abound.",
                "You found another little ed!",
                "You've found so many little eds!",
                "Another little ed found by you!"
            };

            return Content(phrases[MvcApplication.Rnd.Next(phrases.Length)] + $"\r\nThat makes {n}.");
        }
    }

    public class LittleEd {
        public static FileInfo LilEdFindersFile = new FileInfo(HttpContext.Current.Server.MapPath("~/App_Data/liledfinders.json"));

        public static List<string> LilEdTokens = new List<string>();
        public static Dictionary<Guid, int> LilEdFinders = new Dictionary<Guid, int>();

        static LittleEd() {
            if (LilEdFindersFile.Exists) LoadFinders();
            else SaveFinders();
        }

        public static void LoadFinders() {
            LilEdFinders = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<Guid, int>>(File.ReadAllText(LilEdFindersFile.FullName));
        }

        public static void SaveFinders() {
            File.WriteAllText(LilEdFindersFile.FullName, Newtonsoft.Json.JsonConvert.SerializeObject(LilEdFinders));
        }

        /// <summary> Returns number of lil eds found by current user. </summary>
        public static int Found(string let) {
            if (!LilEdTokens.Contains(let)) return 0;

            LilEdTokens.Remove(let);

            Guid userId = MvcApplication.CurrentUser.Id;
            if (LilEdFinders.ContainsKey(userId)) LilEdFinders[userId]++;
            else LilEdFinders.Add(userId, 1);

            SaveFinders();

            int n = LilEdFinders[userId];

            if (n > 0) Badge.Award(51, userId); // found a lil ed
            if (n >= 5) Badge.Award(52, userId); // found a lil ed x 5
            if (n >= 10) Badge.Award(53, userId); // found a lil ed x 10
            if (n >= 37) Badge.Award(54, userId); // found a lil ed x 37
            if (n >= 64) Badge.Award(57, userId); // found a lil ed x 64
            if (n >= 209) Badge.Award(58, userId); 
            if (n >= 600) Badge.Award(66, userId);

            return n; 
        }

        /// <summary> Returns a replacement for "ed". </summary>
        public static string GetTag() {
            if (MvcApplication.Rnd.Next(20) > 0) return "ed";

            string newLet = Guid.NewGuid().ToString("N");
            LilEdTokens.Add(newLet);

            return $"<span class='liled' data-let='{newLet}'>ed</span>";
        }

        public static string ProcessMessageBody(string body) {
            body = Regex.Replace(body, @"([\w])ed", $"$1~~~liled~~~");

            while (Regex.IsMatch(body, @"(http://.*)~~~liled~~~")) {
                body = Regex.Replace(body, @"(http://.*)~~~liled~~~", $"$1ed");
            }

            Regex regex = new Regex("~~~liled~~~");
            while (regex.IsMatch(body)) {
                body = regex.Replace(body, $"{(GetTag())}", 1);
            }

            return body;
        }
    }
}
