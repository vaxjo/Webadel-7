using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using Webadel7.Controllers;

namespace Webadel7 {
    public class Message {
        public enum VoteOpt { Up, Down, None };

        public readonly Guid Id;
        public Guid RoomId, AuthorId;
        public Guid? RecipientId;
        public DateTime Date;
        public string Body, OriginalRoomName;
        public bool New;

        public Room Room => Room.Load(RoomId);
        public User Author => User.Load(AuthorId);
        public User Recipient => (RecipientId.HasValue ? User.Load(RecipientId.Value) : null);

        public List<Resource> AttachedFiles => Resource.GetAll().Where(o => o.MessageId == Id).ToList();

        public string ToCitDate => Message.CitDate(Date);

        public JsonMessage ToJson => new JsonMessage(this);

        public static int TotalMessages {
            get {
                DB.WebadelDataContext dc = new DB.WebadelDataContext();
                return dc.Messages.Count();
            }
        }

        public static double AML {
            get {
                DB.WebadelDataContext dc = new DB.WebadelDataContext();
                return dc.Messages.Sum(o => o.body.Length) / dc.Messages.Count();
            }
        }

        internal Message() {
            Id = Guid.NewGuid();
        }
        public Message(Guid messageId, bool isNew) : this(DB.WebadelDataContext.GetProfiledDC().Messages.Single(o => o.id == messageId), isNew) { }
        internal Message(DB.Message dbMessage, bool isNew) {
            Id = dbMessage.id;
            RoomId = dbMessage.roomId;
            AuthorId = dbMessage.authorId;
            RecipientId = dbMessage.recipientId;
            Date = dbMessage.date;
            Body = dbMessage.body;
            New = isNew;
            OriginalRoomName = (dbMessage.Message_OriginalRoom != null ? dbMessage.Message_OriginalRoom.originalRoomName : null);
            // Score = dbMessage.Votes.Sum(o => (o.vote1 ? 1 : -1));
        }
        internal Message(DB.GetUserMessagesResult dbMessage) {
            Id = dbMessage.id;
            RoomId = dbMessage.roomId;
            AuthorId = dbMessage.authorId;
            RecipientId = dbMessage.recipientId;
            Date = dbMessage.date;
            Body = LittleEd.ProcessMessageBody(dbMessage.body);
            New = dbMessage.isNew;
            OriginalRoomName = dbMessage.originalRoomName;
            //Score = dbMessage.score ?? 0;
        }

        public static string CitDate(DateTime date) {
            string datePart = date.ToString("yyMMMd");

            // extend halloween
            if (datePart.EndsWith("Nov1")) datePart = datePart.Replace("Nov1", "Oct32");

            string timePart = date.ToString("h:mm tt").ToLower();
            return datePart + " " + timePart;
        }

        public static void DeleteMessages(List<Guid> messageIds) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();

            // explicitly remove vote records
            dc.Votes.DeleteAllOnSubmit(dc.Votes.Where(o => messageIds.Contains(o.messageId)));

            dc.Messages.DeleteAllOnSubmit(dc.Messages.Where(o => messageIds.Contains(o.id)));
            dc.SubmitChanges();

            Myriads.Cache.Remove("Most Recent Message"); // since we don't know which rooms these messages were in it's easiest to just refresh all rooms
        }

        public static void MoveMessages(List<Guid> messageIds, Guid roomId) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();

            DB.Room moveRoom = dc.Rooms.Single(o => o.id == roomId);

            // rebuilt to preserve message order
            DateTime moveDate = MvcApplication.Now;
            foreach (DB.Message msg in dc.Messages.Where(o => messageIds.Contains(o.id)).OrderBy(o => o.date)) {
                if (msg.Message_OriginalRoom == null) dc.Message_OriginalRooms.InsertOnSubmit(new DB.Message_OriginalRoom() { messageId = msg.id, originalRoomName = msg.Room.name });
                msg.Room = moveRoom;
                msg.date = moveDate;
                moveDate = moveDate.AddSeconds(1); // this is to ensure the message order is preserved
            }

            dc.SubmitChanges();

            Myriads.Cache.Remove("Most Recent Message"); // since we don't know which rooms these messages were in it's easiest to just refresh all rooms
        }

        public static void CullOldMessages(HttpContext httpContext) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            DateTime cullDate = MvcApplication.Now.AddDays(-SystemConfig.MessageCull);
            List<DB.Message> oldMessages = dc.Messages.Where(o => o.date < cullDate).ToList();
            if (oldMessages.Count == 0) return;

            ArchiveMessages(oldMessages, httpContext);

            dc.Messages.DeleteAllOnSubmit(oldMessages);
            dc.SubmitChanges();

            dc.Votes.DeleteAllOnSubmit(oldMessages.SelectMany(o => o.Votes));
            dc.SubmitChanges();

            Room.PostToSystem("Messages culled: " + oldMessages.Count, httpContext);

            Myriads.Cache.Remove("UserRooms", null, httpContext);
            Myriads.Cache.Remove("Rooms", null, httpContext);
            Myriads.Cache.Remove("Most Recent Message", null, httpContext); // since we don't know which rooms these messages were in it's easiest to just refresh all rooms
        }

        /// <summary> Register a vote and return the new score for the specified message (returns null if there are now no votes for that message). </summary>
        public static int? Vote(Guid messageId, Guid userId, VoteOpt voteOpt) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();

            DB.Vote vote = dc.Votes.SingleOrDefault(o => o.messageId == messageId && o.userId == userId);
            if (voteOpt == VoteOpt.None) {
                if (vote != null) dc.Votes.DeleteOnSubmit(vote);
            } else {
                if (vote == null) {
                    vote = new DB.Vote() { messageId = messageId, userId = userId };
                    dc.Votes.InsertOnSubmit(vote);
                }
                vote.datetime = MvcApplication.Now;
                vote.vote1 = (voteOpt == VoteOpt.Up);
            }
            dc.SubmitChanges();

            Myriads.Cache.Remove("UserVotes");
            DB.Message msg = dc.Messages.Single(o => o.id == messageId);
            return (msg.Votes.Count > 0 ? msg.Votes.Sum(o => (o.vote1 ? 1 : -1)) : (int?)null);
        }

        public static void ArchiveMessages(List<DB.Message> messages, HttpContext httpContext) {
            foreach (DB.Message msg in messages.Where(o => o.roomId != SystemConfig.MailRoomId).OrderBy(o => o.date)) {
                string header = "   " + Message.CitDate(msg.date) + " from " + msg.Author.username;
                if (msg.recipientId.HasValue) header += " to " + msg.Recipient.username;
                if (msg.Message_OriginalRoom != null) header += " in " + msg.Message_OriginalRoom.originalRoomName + ">";

                File.AppendAllText(httpContext.Server.MapPath("~/archive/" + msg.roomId + ".txt"), header + "\r\n" + msg.body + "\r\n\r\n");
            }
        }

        /// <summary> Autosave message for user. Set message=null to remove record. </summary>
        public static void Autosave(Guid userId, Guid roomId, string message) {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            DB.Autosave autosave = dc.Autosaves.SingleOrDefault(o => o.userId == userId && o.roomId == roomId);

            if (string.IsNullOrWhiteSpace(message)) {
                if (autosave == null) return;
                dc.Autosaves.DeleteOnSubmit(autosave);
            } else {
                if (autosave == null) {
                    dc.Autosaves.InsertOnSubmit(new DB.Autosave() { userId = userId, roomId = roomId, message = message });
                } else {
                    autosave.message = message;
                }
            }

            dc.SubmitChanges();
        }

        public static string GetAutosaved(Guid userId, Guid roomId) {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            DB.Autosave autosave = dc.Autosaves.SingleOrDefault(o => o.userId == userId && o.roomId == roomId);

            // this is uncached, and while it's called on every Room load it seems plenty fast
            return (autosave != null ? autosave.message : "");
        }
    }

    public struct JsonMessage {
        public Guid id;
        public Guid roomId, authorId;
        public Guid? recipientId;
        public string authorName, recipientName, roomName;
        public string date, body, originalRoomName;
        public long ticks;
        public bool isNew, isBirthday;
        //public int score;
        public List<JsonResource> attachments;

        public JsonMessage(Message m) {
            id = m.Id;
            roomId = m.RoomId;
            roomName = m.Room.Name;
            authorId = m.AuthorId;
            authorName = m.Author.Username;
            isBirthday = m.Author.Profile.IsBirthday;
            recipientId = m.RecipientId;
            recipientName = (m.RecipientId.HasValue ? m.Recipient.Username : null);
            date = m.ToCitDate;
            body = m.Body;
            ticks = m.Date.Ticks;
            isNew = m.New;
            originalRoomName = m.OriginalRoomName;
            //score = m.Score;
            attachments = m.AttachedFiles.Select(o => new JsonResource(o)).ToList();
        }

        public static JsonMessage Create(string body, Guid roomId, Guid authorId, Guid? recipientId) {
            DateTime now = MvcApplication.Now;

            return new JsonMessage() {
                id = Guid.NewGuid(),
                authorId = authorId, authorName = User.Load(authorId).Username,
                recipientId = recipientId, recipientName = (recipientId.HasValue ? User.Load(recipientId.Value).Username : null),
                roomId = roomId, roomName = Room.Load(roomId).Name, originalRoomName = null, // score = 0,
                attachments = new List<JsonResource>(), date = Message.CitDate(now), body = body, ticks = now.Ticks, isNew = true
            };
        }
    }
}