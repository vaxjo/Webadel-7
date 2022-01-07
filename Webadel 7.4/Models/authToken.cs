using System;
using System.Data.Linq;
using System.Linq;
using System.Web;

namespace Webadel7 {
    public class AuthToken {
        public Guid UserId, Token;
        public DateTime LastUse;

        // this should be fast
        public static AuthToken Get(Guid tokenId) {
            AuthToken authToken = (AuthToken)Myriads.Cache.Get("AuthTokens", tokenId, delegate () {
                DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
                var dbAT = dc.AuthTokens.SingleOrDefault(o => o.tokenId == tokenId);
                return (dbAT == null ? null : new AuthToken() { Token = dbAT.tokenId, UserId = dbAT.userId, LastUse = dbAT.lastUse });
            });
            if (authToken == null) return null;

            authToken.LastUse = MvcApplication.Now;

            Myriads.Cache.Add("AuthTokens", tokenId, authToken);

            return authToken;
        }

        public static void Add(AuthToken authToken) {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            dc.AuthTokens.InsertOnSubmit(new DB.AuthToken() { tokenId = authToken.Token, userId = authToken.UserId, lastUse = authToken.LastUse });
            dc.SubmitChanges();
        }

        public static void Remove(Guid tokenId) {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            DB.AuthToken dbToken = dc.AuthTokens.SingleOrDefault(o => o.tokenId == tokenId);
            if (dbToken == null) return;

            dc.AuthTokens.DeleteOnSubmit(dbToken);
            dc.SubmitChanges();

            Myriads.Cache.Remove("AuthTokens", tokenId);
        }

        public static AuthToken New(Guid userId) {
            return new AuthToken() { Token = Guid.NewGuid(), UserId = userId, LastUse = MvcApplication.Now };
        }

        public static void SaveAuthTokens(HttpContext httpContext) {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            foreach (DB.AuthToken at in dc.AuthTokens) {
                try {
                    AuthToken authToken = (AuthToken)Myriads.Cache.Get("AuthTokens", at.tokenId, httpContext);
                    // set updateCheck = never in dbml for AuthToken.LastUser (we don't care about concurrency problems with this field) [jj 13Dec14]
                    // same for userId field, but I don't think this should make any difference [jj 13Dec28]
                    if (authToken != null) at.lastUse = authToken.LastUse;

                } catch (NullReferenceException nrExc) {
                    // sometimes baseHttpContext.ApplicationInstance becomes null during the loop.
                    // I don't know why. 
                    // TODO: after we fix the Optimistic concurrency let's see if this is still a real problem [jj 14Feb11]
                    System.Diagnostics.Debug.WriteLine("NullReferenceException");

                    Room.PostToSystem("NullReferenceException in SaveAuthTokens(). <p>" + nrExc.Message + "</p>", httpContext);
                }
            }

            dc.SubmitChanges(ConflictMode.ContinueOnConflict);

            dc.AuthTokens.DeleteAllOnSubmit(dc.AuthTokens.Where(o => o.lastUse < MvcApplication.Now.AddDays(-8)));
            dc.SubmitChanges(ConflictMode.ContinueOnConflict);
        }
    }
}

