using System;
using System.Data.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Webadel7.Controllers {
    [WebadelAuthorize]
    public class MessagesController : WebadelController {

        public Myriads.JsonNetResult GetLastMessage(Guid roomId) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();

            // exclude anything over ten minutes old - the window for deletion has past
            DB.GetLastMessagesResult m = dc.GetLastMessages().Where(o => o.messageDate > DateTime.Now.AddMinutes(-10)).FirstOrDefault(o => o.roomId == roomId);

            return JsonNet(m == null ? null : m.messageId);
        }

        // this is more of an "undo" now
        public ContentResult DeleteLastMessage(Guid messageId) {
            DB.WebadelDataContext dc = new DB.WebadelDataContext();

            // check if message is still eligible to be deleted (still last and not too old)
            if (!dc.GetLastMessages().Where(o => o.messageDate > DateTime.Now.AddMinutes(-15)).Any(o => o.messageId == messageId)) return Content("1");

            DB.Message dbMessage = dc.Messages.SingleOrDefault(o => o.id == messageId);

            // message can't be found
            if (dbMessage == null) return Content("2");

            // make sure the deleting user is the author
            if (dbMessage.authorId != MvcApplication.CurrentUser.Id) return Content("3");

            // no need to alert the aides every time someone undoes a message
            //Message message = new Message(dbMessage, false);
            //string aidePost = "Following message in " + message.Room.Name + " deleted by " + CurrentUser.Username + ":";
            //aidePost += "<p><small>" + message.ToCitDate + " from " + message.Author.Username + "<br>" + Server.HtmlEncode(message.Body) + "</small></p>";
            //Room.PostToAide(aidePost);

            Message.DeleteMessages(new List<Guid> { messageId });

            return Content("0" + dbMessage.body);
        }
    }
}
