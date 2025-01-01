using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Web;

namespace Webadel7 {
    // as in, "Tag, you're it"
    public class It {
        public static FileInfo ItFIle = new FileInfo(HttpContext.Current.Server.MapPath("~/App_Data/it.json"));

        public Guid ItId { get; set; }
        public DateTime BecameIt { get; set; }
        public bool HasBeenAlerted { get; set; }

        [JsonIgnore]
        public User ItUser => User.Load(ItId);

        [JsonIgnore]
        public TimeSpan ItDuration => DateTime.Now.Subtract(BecameIt);

        static It() {
            if (ItFIle.Exists) return;

            PickIt();
        }

        public void AlertIt() {
            HasBeenAlerted = true;
            File.WriteAllText(ItFIle.FullName, JsonConvert.SerializeObject(this));
            Myriads.Cache.Remove("It", "");
        }

        public static It Get() {
            return (It)Myriads.Cache.Get("It", "", delegate () {
                return JsonConvert.DeserializeObject<It>(File.ReadAllText(ItFIle.FullName));
            });
        }

        public static void SetIt(Guid userId) {
            var it = new It { ItId = userId, BecameIt = DateTime.Now, HasBeenAlerted = false };

            File.WriteAllText(ItFIle.FullName, JsonConvert.SerializeObject(it));

            Badge.Unaward(64); // "It" badge
            Badge.Award(64, it.ItId);

            Myriads.Cache.Remove("It", "");
        }

        /// <summary> Randomly assign someone to be it. </summary>
        public static void PickIt() {
            var users = User.GetAll(true);
            Random rnd = new Random();

            var rndUser = users[rnd.Next(users.Count)];
            SetIt(rndUser.Id);
        }
    }
}
