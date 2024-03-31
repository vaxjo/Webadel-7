using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;

namespace Webadel7.Controllers {
    [WebadelAuthorize]
    public class WordleController : WebadelController {
        public ActionResult Index() => View();

        // just make sure it's a valid word (5-letters, exists in dictionary)
        public Myriads.CallbackResult CheckGuess(string word) {
            if (!Regex.IsMatch(word, "[a-zA-Z]{5}")) return Myriads.CallbackResult.Get(20, "Invalid word.");

            if (!Wordle.IsValidWord(word)) return Myriads.CallbackResult.Get(10, "Word not found in dictionary.");

            return Myriads.CallbackResult.Success;
        }

        public ActionResult ShowGuess(string word) {
            var guess = new Wordle.Guess(word);

            if (DateTime.Now.Month == 4 && DateTime.Now.Day == 1) return View(guess.CheckMaladjusted());

            return View(guess.Check());
        }

        public ActionResult GetActualUse() {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();
            Random rnd = new Random();

            string word = Wordle.GetTodaysWord();

            List<DB.Message> candidates = dc.Messages.Where(o => o.roomId != SystemConfig.MailRoomId && !o.Room.@private).Where(o => o.body.ToUpper().Contains(word)).ToList();
            if (candidates.Count == 0) return Content(""); // then where did the word come from?!

            ViewBag.Word = word;
            return View(candidates[rnd.Next(candidates.Count)]);
        }
    }
}
