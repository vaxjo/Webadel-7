using System;
using System.Data.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Webadel7.Controllers {
    [WebadelAuthorize]
    public class RoomController : WebadelController {
        protected override void OnActionExecuted(ActionExecutedContext filterContext) {
            base.OnActionExecuted(filterContext);

            // every action in this controller amounts to something the end user specifically did (as opposed to those things that might be automated)
            SystemActivity.Update(CurrentUser.Id);
        }

        public ActionResult Index() {
            ViewBag.PageTitle = SystemConfig.SystemName;

            if (CurrentUser.Profile.IsBirthday) Badge.Award(16, CurrentUser.Id); // #16 - it's your birthday

            return View(CurrentUser);
        }

        public ActionResult Index_RoomList() {
            return View(UserRoom.GetAll(CurrentUser.Id));
        }

        public ActionResult Index_MailRecipientSelect() {
            // all users, sorted in reverse last-login order
            List<User> all = Webadel7.User.GetAll();

            // list of recent mail recipient for current user - move recently email users to top of mail select list
            foreach (User recipient in CurrentUser.GetAllMessages(Webadel7.SystemConfig.MailRoomId).Select(o => o.Recipient).Distinct()) {
                if (all.Contains(recipient)) {
                    all.Remove(recipient);
                    all.Insert(0, recipient);
                }
            }

            return View(all);

        }

        public ActionResult Index_ModeratorControls() {
            return View(UserRoom.GetAll(CurrentUser.Id));
        }
        
        public ActionResult Index_RoomList_Dialog() {
            return View(UserRoom.GetAll(CurrentUser.Id));
        }

        public ActionResult Index_SortRooms_Dialog() {
            return View(UserRoom.GetAll(CurrentUser.Id));
        }

        public ActionResult Index_AccountSettings_Dialog() {
            return View(CurrentUser);
        }

        public ActionResult Index_PlonkSettings_Dialog() {
            return View(CurrentUser);
        }
        public ActionResult Index_PlonkSettings_Form() {
            return View(CurrentUser);
        }

        public ActionResult Index_Profile_Dialog() {
            return View(CurrentUser.Profile);
        }

        public ActionResult Index_Help_Dialog() {
            ViewBag.Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return View();
        }

        public ActionResult Index_UserList_Dialog() {
            return View(Webadel7.User.GetAll(false).Where(o => o.Id != SystemConfig.SysopId && o.Id != SystemConfig.AnonymousId).ToList());
        }

        public ActionResult Index_EditUser_Dialog(Guid userId) {
            if (!CurrentUser.Aide && !CurrentUser.CoSysop) return Content("<div class='alert alert-danger'>Not Authorized</div>");
            ViewBag.IsCoSysop = CurrentUser.CoSysop;
            return View(Webadel7.User.Load(userId));
        }

        public class Index_ViewProfile_Dialog_Model {
            public Webadel7.User User;
            public bool CanEdit;

            public Index_ViewProfile_Dialog_Model(User user, bool canEdit) {
                User = user;
                CanEdit = canEdit;
            }

            public int Ups { get { return User.Votes.Count(o => o.Vote == 1); } }
            public int Downs { get { return User.Votes.Count(o => o.Vote == -1); } }

            public List<User> FavoriteUsers {
                get {
                    Dictionary<Guid, int> UserScores = new Dictionary<Guid, int>();
                    foreach (Guid userId in User.Votes.Select(o => o.MessageAuthorId).Distinct()) {
                        int score = User.Votes.Where(o => o.MessageAuthorId == userId).Sum(o => o.Vote);
                        if (score > 0) UserScores.Add(userId, score);
                    }

                    // no positive votes at all
                    if (UserScores.Count == 0) return new List<Webadel7.User>();

                    int bestScore = UserScores.Max(o => o.Value);
                    return UserScores.Where(o => o.Value == bestScore).Select(o => User.Load(o.Key)).ToList();
                }
            }
            public List<User> LeastFavoriteUsers {
                get {
                    Dictionary<Guid, int> UserScores = new Dictionary<Guid, int>();
                    foreach (Guid userId in User.Votes.Select(o => o.MessageAuthorId).Distinct()) {
                        int score = User.Votes.Where(o => o.MessageAuthorId == userId).Sum(o => o.Vote);
                        if (score < 0) UserScores.Add(userId, score);
                    }

                    // no negative votes at all
                    if (UserScores.Count == 0) return new List<Webadel7.User>();

                    int worstScore = UserScores.Min(o => o.Value);
                    return UserScores.Where(o => o.Value == worstScore).Select(o => User.Load(o.Key)).ToList();
                }
            }
        }
        public ActionResult Index_ViewProfile_Dialog(Guid userId) {
            return View(new Index_ViewProfile_Dialog_Model(Webadel7.User.Load(userId), (CurrentUser.Aide || CurrentUser.CoSysop)));
        }

        public EmptyResult UpdateUserNotes(Guid userId, string notes) {
            if (!CurrentUser.Aide && !CurrentUser.CoSysop) return null;

            Webadel7.User user = Webadel7.User.Load(userId);
            user.Notes = notes;
            user.Save();

            return null;
        }

        public ActionResult Index_Room_Dialog(Guid? roomId) {
            ViewBag.IsCosysop = CurrentUser.CoSysop;
            return View(roomId.HasValue ? Room.Load(roomId.Value) : new Room());
        }

        public ActionResult Index_RoomInfo_Dialog(Guid roomId) {
            return View(Room.Load(roomId));
        }

        public ActionResult Index_Search_Dialog() {
            return View();
        }

        [ValidateInput(false)]
        public Myriads.JsonNetResult PreviewMessage(string message, Guid roomId, Guid? recipientId, Guid[] files, bool postAnonymously) {
            JsonMessage previewMessage = JsonMessage.Create(message, roomId, (postAnonymously ? SystemConfig.AnonymousId : CurrentUser.Id), recipientId);
            if (files != null) foreach (Guid fileId in files) previewMessage.attachments.Add(new JsonResource(Resource.Load(fileId)));
            return JsonNet(previewMessage);
        }

        [ValidateInput(false)]
        public Myriads.JsonNetResult PostMessage(string message, Guid roomId, Guid? recipientId, Guid[] files, bool postAnonymously) {
            Room room = Room.Load(roomId);
            if (!room.Anonymous && postAnonymously) throw new Exception("Can't post anonymously in a room that does not allow anonymous posting. Obvs.");
            if (roomId == SystemConfig.MailRoomId && !recipientId.HasValue) throw new Exception("Can't post mail without a recipient. Duh.");

            Message m = room.Post(message, (postAnonymously ? SystemConfig.AnonymousId : CurrentUser.Id), recipientId);

            if (files != null) {
                foreach (Guid fileId in files) {
                    Resource r = Resource.Load(fileId);
                    r.MessageId = m.Id;
                    r.Save();
                }
            }

            Badge.AwardBadges(m);

            return JsonNet(m.ToJson);
        }

        [ValidateInput(false)]
        public EmptyResult AutoSave(string message, Guid roomId, Guid? recipientId, Guid[] files, bool postAnonymously) {
            Message.Autosave(MvcApplication.CurrentUser.Id, roomId, message);
            return null;
        }

        public Myriads.JsonNetResult GetRoom(Guid roomId) {
            // DB.WebadelDataContext dc = new DB.WebadelDataContext();
            UserRoom ur = UserRoom.Load(CurrentUser.Id, roomId);
            if (ur == null) return JsonNet(new { error = "noroom" }); // if the client ends up with a bad room id let's not throw an exception

            return JsonNet(new {
                id = ur.RoomId,
                name = ur.Room.Name,
                isMail = (ur.RoomId == SystemConfig.MailRoomId),
                canEdit = ur.Room.CanUserEdit(CurrentUser), // CurrentUser.ModeratingRooms.Contains(ur.RoomId) || CurrentUser.Aide || CurrentUser.CoSysop,
                isForgotten = ur.Forgotten,
                isNSFW = ur.NSFW,
                isAnonymous = ur.Room.Anonymous,
                autosavedMessage = Message.GetAutosaved(MvcApplication.CurrentUser.Id, roomId),
                error = ""
            });
        }

        public Myriads.JsonNetResult GetNextRoom(Guid? roomId) {
            return JsonNet(CurrentUser.NextRoom(roomId));
        }

        public Myriads.JsonNetResult GetAllMessages(Guid roomId) {
            return JsonNet(CurrentUser.GetAllMessages(roomId).Select(o => o.ToJson));
        }

        public Myriads.JsonNetResult GetNewMessages(Guid roomId) {
            return JsonNet(CurrentUser.GetNewMessages(roomId).Select(o => o.ToJson));
        }

        public Myriads.JsonNetResult GetNewestMessages(Guid roomId, long newestMessageTicks) {
            return JsonNet(CurrentUser.GetNewestMessages(roomId, new DateTime(newestMessageTicks)).Select(o => o.ToJson));
        }

        public Myriads.JsonNetResult GetMoreMessages(Guid roomId, long? lastMessageTicks) {
            return JsonNet(CurrentUser.GetMoreMessages(roomId, (lastMessageTicks.HasValue ? new DateTime(lastMessageTicks.Value) : MvcApplication.Now)).Select(o => o.ToJson));
        }

        public Myriads.JsonNetResult Search(string q, Guid? roomId, Guid? userId) => JsonNet(CurrentUser.Search(q, roomId, userId).OrderBy(o => o.Room.Name).ThenBy(o => o.Date).Select(o => o.ToJson));

        public ActionResult Forget(Guid roomId, bool forget) {
            UserRoom ur = UserRoom.Load(CurrentUser.Id, roomId);
            ur.Forgotten = forget;
            ur.Save();

            return View(ur);
        }
        public ActionResult ExpireForget(Guid roomId, int timeout) {
            UserRoom ur = UserRoom.Load(CurrentUser.Id, roomId);
            return View(ur.SetForgottenExpiration(timeout));
        }

        public EmptyResult NSFW(Guid roomId, bool nsfw) {
            UserRoom ur = UserRoom.Load(CurrentUser.Id, roomId);
            ur.NSFW = nsfw;
            ur.Save();
            return null;
        }

        public EmptyResult SetPointer(Guid? roomId, long? ticks) {
            if (!roomId.HasValue || !ticks.HasValue) return null; // sometimes there's nothing to set

            UserRoom ur = UserRoom.Load(CurrentUser.Id, roomId.Value);
            ur.SetPointer(new DateTime(ticks.Value));

            return null;
        }

        public EmptyResult RestoreLastPointer(Guid? roomId) {
            if (!roomId.HasValue) return null; // sometimes there's nothing to set

            UserRoom ur = UserRoom.Load(CurrentUser.Id, roomId.Value);
            ur.RestoreLastPointer();
            return null;
        }

        public Myriads.JsonNetResult ChangePassword(string currentPassword, string newPassword, string confirmPassword) {
            List<string> errors = new List<string>();
            if (string.IsNullOrWhiteSpace(currentPassword)) errors.Add("currentpasswordempty");
            if (string.IsNullOrWhiteSpace(newPassword)) errors.Add("newpasswordempty");

            Webadel7.User testUser = Webadel7.User.Load(currentPassword);
            if (testUser == null || testUser.Id != CurrentUser.Id) errors.Add("passwordwrong");

            if (newPassword != confirmPassword) errors.Add("confirm");
            if (CurrentUser.Username.Trim() == newPassword.Trim()) errors.Add("pwisusername");

            User conflictUser = Webadel7.User.Load(newPassword);
            if (conflictUser != null && conflictUser.Id != CurrentUser.Id) errors.Add("badpw");

            if (errors.Count > 0) return JsonNet(string.Join(",", errors));

            CurrentUser.ChangePassword(newPassword);
            return JsonNet(""); // return empty on success
        }

        public Myriads.CallbackResult ReorderRooms(string roomIds) {
            List<Guid> orderedRoomIds = roomIds.Split(',').Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => new Guid(o)).ToList();
            UserRoom.ReorderRooms(CurrentUser.Id, orderedRoomIds);

            return Myriads.CallbackResult.Success;
        }

        [ValidateInput(false)]
        public EmptyResult AccountSettings(string username, string email, User.AttachmentDisplayType attachmentDisplay, bool enableSwipe, bool enablePredictiveText) {
            CurrentUser.Username = username;
            CurrentUser.Email = email;
            CurrentUser.AttachmentDisplay = attachmentDisplay;
            CurrentUser.EnableSwipe = enableSwipe;
            CurrentUser.EnablePredictiveText = enablePredictiveText;
            CurrentUser.Save();
            return null;
        }

        public EmptyResult PlonkUser(Guid userId) {
            Plonk.Add(MvcApplication.CurrentUser.Id, userId);
            return null;
        }

        public EmptyResult UnplonkUser(Guid userId) {
            Plonk.Remove(MvcApplication.CurrentUser.Id, userId);
            return null;
        }

        [ValidateInput(false)]
        public EmptyResult UpdateProfile(string email, string location, string website, string pronouns, string bio, int? birthdate_month, int? birthdate_day, int? birthdate_year) {
            UserProfile profile = CurrentUser.Profile;

            profile.Bio = bio;
            profile.Email = email;
            profile.Location = location;
            profile.Website = website;
            profile.Pronouns = pronouns;

            DateTime? birthdate = null;
            if (birthdate_month.HasValue && birthdate_day.HasValue) {
                // set to 1900 if they don't specify a year
                birthdate = new DateTime(birthdate_year.HasValue ? birthdate_year.Value : 1900, birthdate_month.Value, birthdate_day.Value);
            }
            profile.Birthdate = birthdate;

            profile.Save();

            return null;
        }

        [ValidateInput(false)]
        public Myriads.JsonNetResult UpdateRoom(Guid? roomId, string name, string info, string @private, string anonymous, string requiresTrust, string hidden, string permanent, List<Guid> moderators, List<Guid> invitees) {
            Room room = (roomId.HasValue ? Room.Load(roomId.Value) : Room.NewRoom(name));
            // verify current user has access to edit
            if (roomId.HasValue && !room.CanUserEdit(CurrentUser)) return JsonNet(room.Id);
            string originalRoomDesc = (roomId.HasValue ? room.ToString() : "");

            room.Name = name;
            room.Info = info;
            room.Private = (@private != null);
            room.Anonymous = (anonymous != null);
            room.RequiresTrust = (requiresTrust != null);
            room.Hidden = (hidden != null);
            room.Permanent = (permanent != null);
            room.Save();

            if (moderators == null) moderators = new List<Guid>();
            if (invitees == null) invitees = new List<Guid>();

            // if new room then ensure the current user is moderator (and inivted if private)
            if (!roomId.HasValue) {
                if (!moderators.Contains(CurrentUser.Id)) moderators.Add(CurrentUser.Id);
                if (room.Private && !invitees.Contains(CurrentUser.Id)) invitees.Add(CurrentUser.Id);
            }

            room.SetModerators(moderators);
            room.SetInvitees(invitees);

            if (roomId.HasValue) Room.PostToAide("Room (" + originalRoomDesc + ") modified to (" + room.ToString() + ") by " + CurrentUser.Username + ".");
            else {
                Badge.Award(8, CurrentUser.Id); // #8 - created a room
                Room.PostToAide("New Room (" + room.ToString() + ") created by " + CurrentUser.Username + ".");
            }

            return JsonNet(room.Id);
        }

        public EmptyResult DeleteRoom(Guid roomId) {
            Room room = Room.Load(roomId);
            if (!room.CanUserEdit(CurrentUser)) return null;

            Room.PostToAide("Room (" + room.ToString() + ") deleted by " + CurrentUser.Username + ".");
            room.Delete();
            return null;
        }

        public Myriads.CallbackResult RemoveBadge(Guid userId, int badgeId) {
            if (!CurrentUser.CoSysop) return Myriads.CallbackResult.Unauthorized;
            
            Badge.Unaward(badgeId, userId);
            return Myriads.CallbackResult.Success;
        }

        public ContentResult UpdateUser(Guid userId, string username, string email, string password, string trusted, string aide, string cosysop, string twit) {
            if (!CurrentUser.Aide && !CurrentUser.CoSysop) return Content("You shouldn't be here, you nerd.");

            if (!string.IsNullOrWhiteSpace(password)) {
                User conflictUser = Webadel7.User.Load(password);
                if (conflictUser != null && conflictUser.Id != userId) return Content("That password is in use.");
            }

            User user = Webadel7.User.Load(userId);
            string originalUserDesc = user.ToString();

            user.Username = username;
            user.Email = email;
            user.Trusted = (trusted != null);
            user.Aide = (aide != null);
            user.CoSysop = (cosysop != null);
            user.Twit = (twit != null);
            user.Save();

            Room.PostToAide("User (" + originalUserDesc + ") modified to (" + user.ToString() + ") by " + CurrentUser.Username + ".");

            if (!string.IsNullOrWhiteSpace(password)) user.ChangePassword(password);

            return Content("");
        }

        public EmptyResult DeleteUser(Guid userId) {
            if (!CurrentUser.Aide && !CurrentUser.CoSysop) return null;

            User user = Webadel7.User.Load(userId);
            Room.PostToAide("User (" + user.ToString() + ") deleted by " + CurrentUser.Username + ".");
            user.Delete();

            return null;
        }

        public Myriads.JsonNetResult MoveMessages(Guid? moveRoomId, string newRoomName, string selectedMessages) {
            List<Message> messages = CurrentUser.GetMessages(selectedMessages.Split(',').Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => new Guid(o)).ToList());
            if (messages.Count == 0) return JsonNet("emptyset");

            Room sourceRoom = messages.First().Room;
            if (sourceRoom.Id == SystemConfig.MailRoomId) return JsonNet("invalidmail"); // can't delete or move from mail 
            if (!sourceRoom.CanUserEdit(CurrentUser)) return JsonNet("unauthorized"); // user is unauthorized to move messages from this room

            if (!moveRoomId.HasValue && !string.IsNullOrWhiteSpace(newRoomName)) {
                Room newRoom = Room.NewRoom(newRoomName.Trim());
                moveRoomId = newRoom.Id;
            }

            string aidePost = "";
            if (moveRoomId.HasValue) {
                Message.MoveMessages(messages.Select(o => o.Id).ToList(), moveRoomId.Value);
                aidePost = "Following message(s) in " + sourceRoom + " moved to " + Room.Load(moveRoomId.Value).Name + " by " + CurrentUser.Username + ":";
            } else {
                Message.DeleteMessages(messages.Select(o => o.Id).ToList());
                aidePost = "Following message(s) in " + sourceRoom + " deleted by " + CurrentUser.Username + ":";
            }

            // don't post deletions from aide in aide (but do post moves from aide)
            if (sourceRoom.Id != SystemConfig.AideRoomId || moveRoomId.HasValue) {
                foreach (Message m in messages)
                    aidePost += "<p><small>" + m.ToCitDate + " from " + m.Author.Username + "<br>" + Server.HtmlEncode(m.Body) + "</small></p>";
                Room.PostToAide(aidePost);
            }

            return JsonNet(""); // return empty on success
        }

        [HttpPost]
        public Myriads.JsonNetResult UploadResource(HttpPostedFileBase file) {
            if (file == null) return null;

            Resource r = Resource.UploadResource(file);
            return JsonNet(r);
        }

        public Myriads.JsonNetResult Vote(Guid messageId, Message.VoteOpt vote) {
            return JsonNet(Message.Vote(messageId, CurrentUser.Id, vote));
        }

        public Myriads.JsonNetResult GetVotes(string messageIds) {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            List<Guid> messages = messageIds.Split(',').Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => new Guid(o)).ToList();

            // get messages scores 
            Dictionary<Guid, int> scores = dc.Messages.Where(o => messages.Contains(o.id)).Where(o => o.Votes.Count > 0).ToDictionary(o => o.id, o => (int)o.Votes.Sum(v => (v.vote1 ? 1 : -1)));

            // get vote counts
            Dictionary<Guid, int> counts = dc.Messages.Where(o => messages.Contains(o.id)).Where(o => o.Votes.Count > 0).ToDictionary(o => o.id, o => o.Votes.Count);

            // get current user's votes for these messages
            Dictionary<Guid, bool> userVotes = dc.Votes.Where(o => messages.Contains(o.messageId) && o.userId == CurrentUser.Id).ToDictionary(o => o.messageId, o => o.vote1);

            return JsonNet(new { scores, counts, userVotes });
        }

        public ActionResult GetVoters(Guid messageId) {
            if (!CurrentUser.CoSysop) return Content(""); // removed: !CurrentUser.Aide && 

            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            return View(dc.Votes.Where(o => o.messageId == messageId).ToList());
        }

        public ActionResult GetIPUsers(string ip) {
            if (!CurrentUser.Aide) return Content(""); // removed: !CurrentUser.Aide && 

            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            var usernames = dc.User_IPs.Where(o => o.ip == ip).Select(o => o.User.username).Distinct();
            return Content(ip + " - " + string.Join(", ", usernames));
        }

        public ActionResult Facebook_Dialog(string url) {
            ViewBag.Url = url;
            return View();
        }
    }
}

