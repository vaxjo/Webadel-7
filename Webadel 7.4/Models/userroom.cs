using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Webadel7 {
    public class UserRoom {
        public Guid UserId, RoomId;
        public bool HasAccess, Forgotten, NSFW, Moderator, Noticed;

        public Room Room => Room.Load(RoomId);
        public User User => User.Load(UserId);

        internal UserRoom(DB.UserRoom dbUserRoom) {
            UserId = dbUserRoom.userId;
            RoomId = dbUserRoom.roomId;

            HasAccess = dbUserRoom.hasAccess;
            Forgotten = dbUserRoom.forgotten;
            NSFW = dbUserRoom.nsfw;
            Moderator = dbUserRoom.moderator;
            Noticed = dbUserRoom.noticed;
        }

        public DateTime SetForgottenExpiration(int timeout) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();
            DB.UserRoom ur = dc.UserRooms.Single(o => o.userId == UserId && o.roomId == RoomId);
            ur.forgottenExpiration = MvcApplication.Now.AddDays(timeout);
            dc.SubmitChanges();

            Myriads.Cache.Remove("UserRooms", "Next Expiration");

            return ur.forgottenExpiration.Value;
        }

        public void Save() {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            DB.UserRoom ur = dc.UserRooms.Single(o => o.userId == UserId && o.roomId == RoomId);
            ur.forgotten = Forgotten;
            ur.hasAccess = HasAccess;
            ur.moderator = Moderator;
            ur.noticed = Noticed;
            ur.nsfw = NSFW;
            dc.SubmitChanges();

            MvcApplication.LastSystemChange = MvcApplication.Now;
            Myriads.Cache.Remove("UserRooms", UserId);
            Myriads.Cache.Remove("Rooms"); // this forces the rooms to reload their list of moderators
        }

        public void SetPointer(DateTime newPointer) {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            DB.UserRoom ur = dc.UserRooms.Single(o => o.userId == UserId && o.roomId == RoomId);
            if (ur.pointer == newPointer) return;

            ur.pointerLast = ur.pointer;
            ur.pointer = newPointer;
            dc.SubmitChanges();
        }

        /// <summary> We do this to facilitate ungoto. </summary>
        public void RestoreLastPointer() {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            DB.UserRoom ur = dc.UserRooms.Single(o => o.userId == UserId && o.roomId == RoomId);
            if (ur.pointer == ur.pointerLast) return;

            ur.pointer = ur.pointerLast;
            dc.SubmitChanges();
        }

        public override string ToString() {
            return Room.Name + "/" + User.Username;
        }

        /// <summary> Return a list of userrooms that are available to the specified user. </summary>
        public static List<UserRoom> GetAll(Guid userId) {
            return (List<UserRoom>)Myriads.Cache.Get("UserRooms", userId, delegate () {
                DB.WebadelDataContext dc = new DB.WebadelDataContext();
                return dc.UserRooms.Where(o => o.userId == userId)
                    .Where(o => o.hasAccess || !o.Room.@private)
                    .Where(o => o.noticed || !o.Room.hidden)
                    .Where(o => o.User.trusted || !o.Room.requiresTrust)
                    .OrderBy(o => o.sort).Select(o => new UserRoom(o)).ToList();
            });
        }

        public static UserRoom Load(Guid userId, Guid roomId) {
            return GetAll(userId).SingleOrDefault(o => o.RoomId == roomId);
        }

        public static void ReorderRooms(Guid userId, List<Guid> orderedRoomIds) {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            short i = 1;
            foreach (Guid roomid in orderedRoomIds) {
                var ur = dc.UserRooms.SingleOrDefault(o => o.userId == userId && o.roomId == roomid);
                ur.sort = i++;
            }
            dc.SubmitChanges();
            MvcApplication.LastSystemChange = MvcApplication.Now;
            Myriads.Cache.Remove("UserRooms", userId);
        }

        /// <summary> Periodically check for forgotten room expirations and clear them. </summary>
        public static void UnexpireForgotten() {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();

            DateTime? nextExpiration = (DateTime?)Myriads.Cache.Get("UserRooms", "Next Expiration", delegate () {
                DB.UserRoom ur = dc.UserRooms.Where(o => o.forgottenExpiration.HasValue).OrderBy(o => o.forgottenExpiration).FirstOrDefault();
                return (ur != null ? ur.forgottenExpiration.Value : (DateTime?)null);
            });

            if (!nextExpiration.HasValue || nextExpiration > MvcApplication.Now) return; // nothing to expire

            foreach (DB.UserRoom ur in dc.UserRooms.Where(o => o.forgottenExpiration < MvcApplication.Now)) {
                ur.forgottenExpiration = null;
                ur.forgotten = false;
            }
            dc.SubmitChanges();

            Myriads.Cache.Remove("UserRooms");
        }

    }
}
