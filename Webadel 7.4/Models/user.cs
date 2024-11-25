using System;
using System.Collections.Generic;
using System.Data.Linq;
using System.Linq;
using System.Transactions;
using System.Web;
using System.Web.UI.WebControls;

namespace Webadel7 {
    public class User {
        public enum AttachmentDisplayType { Thumbnail, Full, Half, Text }

        public readonly Guid Id;
        public string Username;
        public bool Trusted, Aide, CoSysop, Twit, Muted, Disabled;
        public string Email, Notes;
        public AttachmentDisplayType AttachmentDisplay;
        public bool EnableSwipe, EnablePredictiveText, EnableVoting;
        public DateTime LastActivity, Created;
        public Dictionary<string, string> Misc;

        public UserProfile Profile => UserProfile.Load(Id);

        /// <summary> User is CoSysop and is local. This should only ever be Vaxjo. </summary>
        public bool SysOp => CoSysop && (HttpContext.Current.Request.IsLocal || HttpContext.Current.Request.UserHostAddress == "68.168.175.132");

        public List<Guid> ModeratingRooms => (List<Guid>)Myriads.Cache.Get("Users", "moderatedRooms", delegate () {
            //DB.WebadelDataContext dc = new DB.WebadelDataContext();
            //return dc.UserRooms.Where(o => o.userId == Id && o.moderator).Select(o => o.roomId).ToList();
            return UserRoom.GetAll(Id).Where(o => o.Moderator).Select(o => o.RoomId).ToList();
        });

        public List<User> PlonkedUsers => Plonk.GetAll().Where(o => o.UserId == Id).Select(o => User.Load(o.PlonkedUserId)).ToList();

        /// <summary> List of users who have plonked this user. </summary>
        public List<User> PlonkedBy => User.GetAll().Where(o => o.PlonkedUsers.Contains(this)).ToList();

        public List<User_PrevAlias> PrevousAliases => User_PrevAlias.GetAll().Where(o => o.UserId == Id).ToList();

        public List<UserVote> Votes => (List<UserVote>)Myriads.Cache.Get("UserVotes", Id, delegate () {
            return UserVote.GetAll(Id);
        }, TimeSpan.FromSeconds(300));

        /// <summary> True if user has logged on since "active user cutoff" time (or is sysop/anonymous). </summary>
        public bool IsActiveUser => (LastActivity > SystemConfig.ActiveUserCutoff || Id == SystemConfig.SysopId || Id == SystemConfig.AnonymousId);

        internal User(DB.User dbUser) {
            Id = dbUser.id;
            Username = dbUser.username;
            Trusted = dbUser.trusted;
            Aide = dbUser.aide;
            CoSysop = dbUser.cosysop;
            Twit = dbUser.twit;
            Muted = dbUser.muted;
            Disabled = dbUser.disabled;
            Email = dbUser.email;
            Notes = dbUser.notes;
            AttachmentDisplay = (AttachmentDisplayType)dbUser.attachmentDisplay;
            EnableSwipe = dbUser.enableSwipe;
            EnablePredictiveText = dbUser.enablePredictiveText;
            EnableVoting = dbUser.enableVoting;
            LastActivity = dbUser.lastActivity;
            Created = dbUser.created;
            Misc = string.IsNullOrWhiteSpace(dbUser.miscDictionary) ? new Dictionary<string, string>() : Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(dbUser.miscDictionary);
        }

        public void Save() {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            DB.User dbUser = dc.Users.Single(o => o.id == Id);

            // track old aliases
            if (dbUser.username != Username) User_PrevAlias.Add(Id, dbUser.username);

            dbUser.username = Username;
            dbUser.trusted = Trusted;
            dbUser.aide = Aide;
            dbUser.cosysop = CoSysop;
            dbUser.twit = Twit;
            dbUser.muted = Muted;
            dbUser.disabled = Disabled;
            dbUser.email = Email;
            dbUser.notes = Notes;
            dbUser.attachmentDisplay = (byte)AttachmentDisplay;
            dbUser.enableSwipe = EnableSwipe;
            dbUser.enablePredictiveText = EnablePredictiveText;
            dbUser.enableVoting = EnableVoting;
            dbUser.miscDictionary = Newtonsoft.Json.JsonConvert.SerializeObject(Misc);

            dc.SubmitChanges();

            Myriads.Cache.Remove("Users");
        }

        public void Delete() {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            DB.User dbUser = dc.Users.Single(o => o.id == Id);
            dbUser.deleted = true;
            dc.SubmitChanges();

            Myriads.Cache.Remove("Users");
        }

        public void RecordIP(string ip) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();

            if (!dc.User_IPs.Any(o => o.ip == ip && o.userId == Id)) {
                dc.User_IPs.InsertOnSubmit(new DB.User_IP() { userId = Id, ip = ip });
                dc.SubmitChanges();
            }
        }

        public List<string> GetIPs() {
            return (List<string>)Myriads.Cache.Get("User IPs", Id, delegate () {
                DB.WebadelDataContext dc = new DB.WebadelDataContext();
                return dc.User_IPs.Where(o => o.userId == Id).Distinct().Select(o => o.ip).OrderBy(o => o).ToList();
            }, TimeSpan.FromMinutes(20));
        }

        public Guid NextRoom(Guid? currentRoomId) {
            Dictionary<Guid, int> newMessages = GetNumNewMessages(!MvcApplication.IsWorkComputer);

            // if all forgotten, return the first room
            if (newMessages.All(o => UserRoom.Load(Id, o.Key).Forgotten)) return newMessages.First().Key;

            // no new messages anywhere
            if (!newMessages.Any(o => o.Value > 0)) return newMessages.First(o => !UserRoom.Load(Id, o.Key).Forgotten).Key;

            bool foundCurrentRoom = false;
            foreach (var kv in newMessages) {
                if (kv.Key == currentRoomId) {
                    foundCurrentRoom = true;
                    continue;
                }

                if (foundCurrentRoom && kv.Value > 0 && !UserRoom.Load(Id, kv.Key).Forgotten) return kv.Key;
            }

            return (newMessages.Any(o => o.Value > 0 && !UserRoom.Load(Id, o.Key).Forgotten) ? newMessages.First(o => o.Value > 0 && !UserRoom.Load(Id, o.Key).Forgotten).Key : newMessages.First(o => !UserRoom.Load(Id, o.Key).Forgotten).Key);
        }

        /// <summary> Return a dictionary of user's rooms with the number of new messages in each room. </summary>
        public Dictionary<Guid, int> GetNumNewMessages(bool includeNSFW = true) {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            return dc.GetNumNewMessages(Id, includeNSFW, false).OrderBy(o => o.sort).ToDictionary(o => o.roomId, o => o.numNew);
        }

        public List<Message> GetMessages(List<Guid> messageIds) {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            return dc.GetUserMessages(Id, true, true).Where(o => messageIds.Contains(o.id)).Select(o => new Message(o)).ToList();
        }

        public List<Message> GetAllMessages(Guid roomId) {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            return dc.GetUserMessages(Id, true, true).Where(o => o.roomId == roomId).OrderBy(o => o.date).Select(o => new Message(o)).ToList();
        }

        public List<Message> GetNewMessages(Guid roomId) {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();

            // union the last old message and all new messages
            var lastOld = dc.GetUserMessages(Id, true, true).Where(o => o.roomId == roomId && !o.isNew).OrderByDescending(o => o.date).Take(1);
            var newMsgs = dc.GetUserMessages(Id, true, true).Where(o => o.roomId == roomId && o.isNew);
            List<Message> newMessages = lastOld.Union(newMsgs).OrderBy(o => o.date).Select(o => new Message(o)).ToList();

            return (newMessages.Count == 1 && !newMessages.First().New ? new List<Message>() : newMessages);
        }

        public List<Message> GetTodayMessages(Guid roomId) {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            return dc.GetUserMessages(Id, true, true).Where(o => o.roomId == roomId && o.date >= DateTime.Now.Date).OrderBy(o => o.date).Select(o => new Message(o)).ToList();
        }

        /// <summary> Get the newest messages after the specified date. </summary>
        public List<Message> GetNewestMessages(Guid roomId, DateTime lastMessageDate) {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            return dc.GetUserMessages(Id, true, true).Where(o => o.roomId == roomId && o.date > lastMessageDate).OrderBy(o => o.date).Select(o => new Message(o)).ToList();
        }

        public Guid? GetMostRecentMessageId(Guid roomId) {
            return (Guid?)Myriads.Cache.Get("Most Recent Message", roomId, delegate () {
                DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
                IQueryable<DB.GetUserMessagesResult> messages = dc.GetUserMessages(Id, true, true).Where(o => o.roomId == roomId).OrderByDescending(o => o.date);
                return messages.Count() > 0 ? messages.First().id : (Guid?)null;
            });
        }

        /// <summary> Get the newest N messages older than the specified date. </summary>
        public List<Message> GetMoreMessages(Guid roomId, DateTime lastMessageDate, int howMany = 5) {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();

            return dc.GetUserMessages(Id, true, true).Where(o => o.roomId == roomId && o.date < lastMessageDate)
                .OrderByDescending(o => o.date)
                .Take(howMany)
                .OrderBy(o => o.date)
                .Select(o => new Message(o)).ToList();
        }

        public List<Message> Search(string q, Guid? roomId, Guid? userId) {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();

            IQueryable<DB.GetUserMessagesResult> messageSet = dc.GetUserMessages(Id, (roomId.HasValue || !MvcApplication.IsWorkComputer), true);
            if (roomId.HasValue) messageSet = messageSet.Where(o => o.roomId == roomId.Value);
            if (userId.HasValue) messageSet = messageSet.Where(o => o.authorId == userId.Value || o.recipientId == userId.Value);

            // AND the components of the seach parameter
            foreach (string q_term in q.Split(' ').Where(o => !string.IsNullOrWhiteSpace(o))) {
                string subq_temp = q_term;
                messageSet = messageSet.Where(o => o.body.Contains(subq_temp));
            }

            return messageSet.Select(o => new Message(o)).ToList();
        }

        public void ChangePassword(string newPassword) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            DB.User dbUser = dc.Users.Single(o => o.id == Id);
            dbUser.password = Myriads.Hash.GetMD5HashString(newPassword);
            dc.SubmitChanges();
        }

        /// <summary> Get all of the users [cached]. </summary>
        public static List<User> GetAll(bool onlyActive = true) {
            List<User> all = (List<User>)Myriads.Cache.Get("Users", "all", delegate () {
                MvcApplication.LastSystemChange = MvcApplication.Now;

                // from: http://stackoverflow.com/a/1220812/3140552
                // an attempt to prevent the rare "transaction deadlocked" error
                using (var txn = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions { IsolationLevel = IsolationLevel.ReadUncommitted })) {
                    DB.WebadelDataContext dc = new DB.WebadelDataContext();
                    return dc.Users.Where(o => !o.deleted).OrderByDescending(o => o.lastActivity).Select(o => new User(o)).ToList();
                }

            }, TimeSpan.FromMinutes(10)); // kill this every ten minutes to keep the "lastActivity" value more or less accurate

            if (onlyActive) all = all.Where(o => o.IsActiveUser && !o.Disabled).ToList();

            return all;
        }

        public static User Load(Guid id) {
            return GetAll(false).SingleOrDefault(o => o.Id == id);
        }

        public static User Get(string email) {
            return GetAll(false).SingleOrDefault(o => (o.Email ?? "").ToLower() == email.ToLower());
        }

        public static User Load(string password) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();

            // local only hack to fake a log in as someone else without knowing their pw (enter ":username")
            if (HttpContext.Current != null && HttpContext.Current.Request.IsLocal && password.StartsWith(":")) {
                DB.User devUser = dc.Users.FirstOrDefault(o => o.username.Trim().ToLower() == password.Substring(1).Trim().ToLower());
                if (devUser != null) return User.Load(devUser.id);
            }

            DB.User user = dc.Users.FirstOrDefault(o => o.password == Myriads.Hash.GetMD5HashString(password) && !o.deleted && !o.disabled);
            if (user == null) return null;

            return User.Load(user.id);
        }

        public static User NewUser(string username, string password, string email) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            DB.User dbUser = new DB.User() { id = Guid.NewGuid(), aide = false, cosysop = false, twit = false, created = MvcApplication.Now, email = email, lastActivity = MvcApplication.Now, password = Myriads.Hash.GetMD5HashString(password), trusted = false, username = username };
            dc.Users.InsertOnSubmit(dbUser);

            DateTime pointer = MvcApplication.Now.AddDays(-3); // every new user gets "3" days worth of new messages to keep them busy
                                                               // get sysop account's userrooms and use them as a template
            foreach (DB.UserRoom ur in dc.UserRooms.Where(o => o.userId == SystemConfig.SysopId)) {
                dc.UserRooms.InsertOnSubmit(new DB.UserRoom() {
                    userId = dbUser.id,
                    roomId = ur.roomId,
                    forgotten = false,
                    hasAccess = false,
                    moderator = false,
                    noticed = false,
                    nsfw = false,
                    pointer = pointer,
                    pointerLast = pointer,
                    sort = ur.sort
                });
            }

            dc.SubmitChanges();

            Myriads.Cache.Remove("Users");
            Myriads.Cache.Remove("UserRooms");
            return User.Load(dbUser.id);
        }

        /// <summary> Create a one-time login token in case user has forgotten pw. </summary>
        public string CreateLoginToken() {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();

            DB.LoginToken newLoginToken = new DB.LoginToken { expiration = DateTime.Now.AddMinutes(30), userId = Id, token = Guid.NewGuid().ToString("N") };

            dc.LoginTokens.InsertOnSubmit(newLoginToken);
            dc.SubmitChanges();

            return newLoginToken.token;
        }

        /// <summary> Return the user associated with this token (if it exists and is not expired). </summary>
        public static User GetUserForToken(string loginToken) {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();

            // remove expired tokens
            dc.LoginTokens.DeleteAllOnSubmit(dc.LoginTokens.Where(o => o.expiration < DateTime.Now));
            dc.SubmitChanges();

            var u = dc.LoginTokens.SingleOrDefault(o => o.token == loginToken);
            if (u == null) return null;

            // remove token after it's used
            dc.LoginTokens.DeleteOnSubmit(u);
            dc.SubmitChanges();

            return User.Load(u.userId);
        }

        /// <summary> True if IP address is allowed to create new accounts. </summary>
        public static bool CanCreateAccount(string ip) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();

            // users associated with this IP address // invalid if muted, twitted, or disabled
            return !dc.User_IPs.Where(o => o.ip == ip).Any(o => o.User.muted || o.User.twit || o.User.disabled);
        }

        public override string ToString() {
            List<string> modifiers = new List<string>();
            if (Trusted) modifiers.Add("trusted"); else modifiers.Add("untrusted");
            if (Aide) modifiers.Add("aide");
            if (CoSysop) modifiers.Add("cosysop");
            if (Twit) modifiers.Add("twit");
            if (Muted) modifiers.Add("muted");
            if (Disabled) modifiers.Add("disabled");

            return Username + " [" + string.Join(", ", modifiers) + "]";
        }
    }

    public class User_PrevAlias {
        public readonly int Id;
        public Guid UserId;
        public string PreviousAlias;
        public DateTime Changed;

        internal User_PrevAlias(DB.User_PrevAlia dbPrevAlias) {
            Id = dbPrevAlias.id;
            UserId = dbPrevAlias.userId;
            PreviousAlias = dbPrevAlias.alias;
            Changed = dbPrevAlias.changed;
        }

        public static List<User_PrevAlias> GetAll() {
            return (List<User_PrevAlias>)Myriads.Cache.Get("User_PrevAlias", "all", delegate () {
                DB.WebadelDataContext dc = new DB.WebadelDataContext();
                return dc.User_PrevAlias.OrderByDescending(o => o.changed).Select(o => new User_PrevAlias(o)).ToList();
            });
        }

        public static void Add(Guid userId, string previousAlias) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            dc.User_PrevAlias.InsertOnSubmit(new DB.User_PrevAlia() { userId = userId, alias = previousAlias, changed = MvcApplication.Now });
            dc.SubmitChanges();

            Myriads.Cache.Remove("User_PrevAlias");
        }

        public override string ToString() {
            return PreviousAlias;
        }
    }

    // collection of votes a particular user has made
    public class UserVote {
        public Guid MessageId;
        public Guid MessageAuthorId;
        public int Vote;

        /// <summary> Return list of votes the specified user has made. </summary>
        public static List<UserVote> GetAll(Guid userId) {
            DataLoadOptions loadOpts = new DataLoadOptions();
            loadOpts.LoadWith<DB.Vote>(o => o.Message);

            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            dc.LoadOptions = loadOpts;

            return dc.Votes.Where(o => o.userId == userId).Select(o => new UserVote() {
                MessageId = o.messageId,
                MessageAuthorId = o.Message.authorId,
                Vote = (o.vote1 ? 1 : -1)
            }).ToList();
        }
    }
}