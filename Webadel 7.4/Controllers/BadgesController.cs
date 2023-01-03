using Myriads;
using System;
using System.Web.Mvc;

namespace Webadel7.Controllers {
    [WebadelAuthorize]
    public class BadgesController : WebadelController {
        protected override void OnActionExecuted(ActionExecutedContext filterContext) {
            base.OnActionExecuted(filterContext);

            // every action in this controller amounts to something the end user specifically did (as opposed to those things that might be automated)
            SystemActivity.Update(CurrentUser.Id);
        }

        public ActionResult Index() {
            if (!CurrentUser.CoSysop) return Redirect("/");
            return View();
        }

        public ActionResult Index_Edit_Modal(int id) => View(Badge.Load(id));

        public ActionResult UnawardBadge(Guid userId, int badgeId) {
            Badge badge = Badge.Load(badgeId);
            User user = Webadel7.User.Load(userId);

            if (Badge.Unaward(badgeId, userId)) return CallbackResult.Success;

            return CallbackResult.Failure;
        }

        [ValidateInput(false)]
        public CallbackResult UpdateBadge(int id, string name, string badgeText, string description, Badge.AssignmentTypes assignmentType, string pendingApproval) {
            Badge badge = Badge.Load(id);

            badge.Name = name;
            badge.BadgeText = badgeText;
            badge.Description = description;
            badge.AssignmentType = assignmentType;
            badge.PendingApproval = (pendingApproval != null);
            badge.Save();

            return CallbackResult.Success;
        }

        public CallbackResult CopyBadge(int badgeId) {
            Badge badge = Badge.Load(badgeId);

            Badge newBadge = Badge.Create(badge.Name + " (copy)", badge.BadgeText, badge.Description, CurrentUser.Id);
            newBadge.PendingApproval = badge.PendingApproval;
            newBadge.Save();

            return CallbackResult.Get(0, newBadge.Id.ToString());
        }

        public CallbackResult ApproveBadge(int id) => Badge.Approve(id) ? CallbackResult.Success : CallbackResult.Failure;

        public CallbackResult RemoveBadge(int id) => Badge.Remove(id) ? CallbackResult.Success : CallbackResult.Failure;

        public CallbackResult AwardBadge(int badgeId, Guid recipientId) => Badge.Award(badgeId, recipientId) ? CallbackResult.Success : CallbackResult.Failure;

        public ActionResult Index_SubmitBadge_Dialog() => View();

        public ActionResult Badge_Modal(int id) => View(Badge.Load(id));

        public ActionResult NominateUser(Guid userId, int badgeId) {
            Badge badge = Badge.Load(badgeId);
            User user = Webadel7.User.Load(userId);

            Room.PostToAide($"{MvcApplication.CurrentUser.Username} has nominated {user.Username} to receive the \"{badge.Name}\" badge.");

            return CallbackResult.Success;
        }

        [ValidateInput(false)]
        public CallbackResult SubmitBadge(string name, string badgeText, string description) => Badge.Submit(name, badgeText, description, MvcApplication.CurrentUser.Id) ? CallbackResult.Success : CallbackResult.Failure;
    }
}
