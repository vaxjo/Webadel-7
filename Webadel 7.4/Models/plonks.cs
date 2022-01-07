using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Webadel7 {
    public class Plonk {
        public Guid UserId, PlonkedUserId;

        internal Plonk(DB.Plonk dbPlonk) {
            UserId = dbPlonk.userId;
            PlonkedUserId = dbPlonk.plonkedUserId;
        }

        public static List<Plonk> GetAll() {
            return (List<Plonk>)Myriads.Cache.Get("Plonk", "all", delegate () {
                DB.WebadelDataContext dc = new DB.WebadelDataContext();
                return dc.Plonks.Select(o => new Plonk(o)).ToList();
            });
        }

        public static void Add(Guid userId, Guid plonkedUserId) {
            // TODO: check for dups
            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            dc.Plonks.InsertOnSubmit(new DB.Plonk() { userId = userId, plonkedUserId = plonkedUserId });
            dc.SubmitChanges();

            Myriads.Cache.Remove("Plonk");
        }

        public static void Remove(Guid userId, Guid plonkedUserId) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            dc.Plonks.DeleteOnSubmit(dc.Plonks.SingleOrDefault(o => o.userId == userId && o.plonkedUserId == plonkedUserId));
            dc.SubmitChanges();

            Myriads.Cache.Remove("Plonk");
        }
    }
}
