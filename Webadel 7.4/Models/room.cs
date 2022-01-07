using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Web;

namespace Webadel7 {
    public class Room {
        public readonly Guid Id;
        public string Name, Info;
        public bool Private, Anonymous, RequiresTrust, Hidden, Permanent;
        protected List<Guid> _moderators, _invitees, _forgetters; // non-cached

        // non-cached; use sparingly
        public List<Guid> Moderators {
            get {
                if (_moderators == null) {
                    DB.WebadelDataContext dc = new DB.WebadelDataContext();
                    _moderators = dc.UserRooms.Where(o => o.roomId == Id && o.moderator).Select(o => o.userId).ToList();
                }
                return _moderators;
            }
        }
        // non-cached; use sparingly
        public List<Guid> Invitees {
            get {
                if (_invitees == null) {
                    DB.WebadelDataContext dc = new DB.WebadelDataContext();
                    _invitees = dc.UserRooms.Where(o => o.roomId == Id && o.hasAccess).Select(o => o.userId).ToList();
                }
                return _invitees;
            }
        }
        // non-cached; use sparingly
        public List<Guid> Forgetters {
            get {
                if (_forgetters == null) {
                    DB.WebadelDataContext dc = new DB.WebadelDataContext();
                    _forgetters = dc.UserRooms.Where(o => o.roomId == Id && o.forgotten).Select(o => o.userId).ToList();
                }
                return _forgetters;
            }
        }

        public Room() { }
        internal Room(DB.Room dbRoom) {
            Id = dbRoom.id;
            Name = dbRoom.name;
            Info = dbRoom.info;
            Private = dbRoom.@private;
            Anonymous = dbRoom.anonymous;
            RequiresTrust = dbRoom.requiresTrust;
            Hidden = dbRoom.hidden;
            Permanent = dbRoom.permanent;
        }

        public Message Post(string body, Guid authorId, Guid? recipientId, HttpContext httpContext = null) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();

            DB.Message dbMessage = new DB.Message() { id = Guid.NewGuid(), authorId = authorId, body = body, date = MvcApplication.Now, recipientId = recipientId, roomId = Id };
            dc.Messages.InsertOnSubmit(dbMessage);
            dc.SubmitChanges();

            Myriads.Cache.Add("Most Recent Message", Id, dbMessage.id, httpContext);

            // send email
            if (recipientId.HasValue) {
                User recipient = Webadel7.User.Load(recipientId.Value);
                if (!string.IsNullOrWhiteSpace(recipient.Email)) {
                    try {
                        User author = Webadel7.User.Load(authorId);
                        SmtpClient smtp = new SmtpClient();
                        var m = new MailMessage("noreply@edsroom.com", recipient.Email, SystemConfig.SystemName + " mail from " + author.Username, body) { IsBodyHtml = true };
                        if (!string.IsNullOrWhiteSpace(author.Email)) m.ReplyToList.Add(author.Email);
                        smtp.Send(m);
                    } catch (Exception e) {
                        Room.PostToSystem("Error trying to send email: " + e.Message);
                    }
                }
            }

            // remove auto-saved message
            Message.Autosave(authorId, Id, null);
            Predictor.ClearCache(authorId);

            return new Message(dbMessage, true);
        }

        public void Save() {
            if (Id == Guid.Empty) throw new Exception("Can't save room create via an empty constructor.");

            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            DB.Room dbUser = dc.Rooms.Single(o => o.id == Id);
            dbUser.name = Name;
            dbUser.info = Info;
            dbUser.@private = Private;
            dbUser.anonymous = Anonymous;
            dbUser.requiresTrust = RequiresTrust;
            dbUser.hidden = Hidden;
            dbUser.permanent = Permanent;
            dc.SubmitChanges();

            Myriads.Cache.Remove("Rooms");
        }

        public void Delete() {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();

            // archive messages that are about to be deleted
            Message.ArchiveMessages(dc.Messages.Where(o => o.roomId == Id).ToList(), System.Web.HttpContext.Current);

            dc.Rooms.DeleteOnSubmit(dc.Rooms.Single(o => o.id == Id));
            // sql cascading should pick up the UserRoom entries
            dc.SubmitChanges();

            Myriads.Cache.Remove("UserRooms");
            Myriads.Cache.Remove("Rooms");
        }

        public void SetModerators(List<Guid> moderatorIds) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            foreach (DB.UserRoom ur in dc.UserRooms.Where(o => o.roomId == Id)) ur.moderator = moderatorIds.Contains(ur.userId);
            dc.SubmitChanges();
            Myriads.Cache.Remove("UserRooms");
        }

        public void SetInvitees(List<Guid> inviteeIds) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            foreach (DB.UserRoom ur in dc.UserRooms.Where(o => o.roomId == Id)) ur.hasAccess = inviteeIds.Contains(ur.userId);
            dc.SubmitChanges();
            Myriads.Cache.Remove("UserRooms");
        }

        /// <summary> Returns true if specified user has access to edit this room. </summary>
        public bool CanUserEdit(User user) {
            if (user.Aide || user.CoSysop) return true;
            UserRoom ur = UserRoom.Load(user.Id, Id);
            return ur.Moderator;
        }

        /// <summary> Get all of the rooms [cached]. </summary>
        public static List<Room> GetAll() {
            return (List<Room>)Myriads.Cache.Get("Rooms", "all", delegate () {
                MvcApplication.LastSystemChange = MvcApplication.Now;
                DB.WebadelDataContext dc = new DB.WebadelDataContext();
                return dc.Rooms.Select(o => new Room(o)).ToList();
            });
        }

        public static Room Load(Guid id) {
            return GetAll().SingleOrDefault(o => o.Id == id);
        }

        public static Room NewRoom(string name) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            DB.Room dbRoom = new DB.Room() { id = Guid.NewGuid(), name = name, info = "", @private = false, anonymous = false, hidden = false, permanent = false, requiresTrust = false };
            dc.Rooms.InsertOnSubmit(dbRoom);

            // create a tx record for each user
            dc.UserRooms.InsertAllOnSubmit(dc.Users.ToList().Select(o => new DB.UserRoom() {
                userId = o.id, roomId = dbRoom.id,
                forgotten = false, hasAccess = false, moderator = false, noticed = false, nsfw = false, pointer = MvcApplication.Now, pointerLast = MvcApplication.Now, sort = 999
            }));

            dc.SubmitChanges();

            // seed the archive file
            if (!Directory.Exists(HttpContext.Current.Server.MapPath("~/archive/"))) Directory.CreateDirectory(HttpContext.Current.Server.MapPath("~/archive/"));
            File.WriteAllText(HttpContext.Current.Server.MapPath("~/archive/" + dbRoom.id + ".txt"), name + ">\r\n\r\n");

            Myriads.Cache.Remove("Rooms");
            Myriads.Cache.Remove("UserRooms");
            return Room.Load(dbRoom.id);
        }

        public static void PostToAide(string body, HttpContext httpContext = null) {
            Room aideRoom = Room.Load(SystemConfig.AideRoomId);
            aideRoom.Post(body, SystemConfig.SysopId, null, httpContext);
        }

        public static void PostToSystem(string body, HttpContext httpContext = null) {
            Room systemRoom = Room.Load(SystemConfig.SystemRoomId);
            systemRoom.Post(body, SystemConfig.SysopId, null, httpContext);
        }

        public static void CullEmptyRooms(HttpContext httpContext) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            List<DB.Room> emptyRooms = dc.Rooms.Where(o => o.Messages.Count == 0 && !o.permanent && o.id != SystemConfig.AideRoomId && o.id != SystemConfig.MailRoomId && o.id != SystemConfig.LobbyRoomId).ToList();
            if (emptyRooms.Count == 0) return;

            Room.PostToAide("Empty room(s) culled:<br><br>" + string.Join("<br>", emptyRooms.Select(o => o.name)), httpContext);

            dc.Rooms.DeleteAllOnSubmit(emptyRooms);
            dc.SubmitChanges();

            Myriads.Cache.Remove("UserRooms", null, httpContext);
            Myriads.Cache.Remove("Rooms", null, httpContext);
        }

        public override string ToString() {
            List<string> modifiers = new List<string>();
            if (Private) modifiers.Add("private"); else modifiers.Add("public");
            if (Anonymous) modifiers.Add("anonymous");
            if (RequiresTrust) modifiers.Add("requires trust");
            if (Hidden) modifiers.Add("hidden");
            if (Permanent) modifiers.Add("permanent");

            return Name + " [" + string.Join(", ", modifiers) + "]";
        }
    }
}