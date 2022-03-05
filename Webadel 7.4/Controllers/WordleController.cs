using System;
using System.Text.RegularExpressions;
using System.Data.Linq;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Web.Mvc;

namespace Webadel7.Controllers {
    [WebadelAuthorize]
    public class WordleController : WebadelController {
        public ActionResult Index() {
            return View();
        }

        public class Check_Model {
            public enum States { Correct, Incorrect, WrongPlace }

            public List<LetterGuess> Guesses;

            public bool IsCorrect => Guesses.All(o => o.State == States.Correct);

            public Check_Model(string testWord) {
                Guesses = new List<LetterGuess>();

                var goalWord = Wordle.GetTodaysWord();

                // populate guesses with test word letters, all incorrect by default
                Guesses = testWord.ToUpper().ToList().Select(o => new LetterGuess { Letter = o, State = States.Incorrect }).ToList();

                // first, find all correct letters (incorrect letters from the goal word go into a list of leftovers)
                int i = 0;
                List<char> leftovers = new List<char>();
                foreach (LetterGuess lg in Guesses) {
                    if (lg.Letter == goalWord[i]) lg.State = States.Correct;
                    else leftovers.Add(goalWord[i]);

                    i++;
                }

                // then look at the non-correct letters again
                foreach (LetterGuess lg in Guesses.Where(o => o.State != States.Correct)) {
                    // if the letter is in the list of leftovers, then it's just in the wrong place; also remove the leftover letter from its list
                    if (leftovers.Contains(lg.Letter)) {
                        lg.State = States.WrongPlace;
                        leftovers.Remove(lg.Letter);
                    }
                }
            }

            public class LetterGuess {
                public char Letter;
                public States State;
            }
        }

        public Myriads.CallbackResult CheckGuess(string word) {
            if (!Regex.IsMatch(word, "[a-zA-Z]{5}")) return Myriads.CallbackResult.Get(20, "Invalid word.");

            if (!Wordle.IsValidWord(word)) return Myriads.CallbackResult.Get(10, "Word not found in dictionary.");

            return Myriads.CallbackResult.Success;
        }

        public ActionResult ShowGuess(string word) {
            var model = new Check_Model(word);
            return View(model);
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

namespace Webadel7 {
    public class Wordle {
        public static FileInfo ValidWordsFile;

        static Wordle() {
            ValidWordsFile = new FileInfo(System.Web.HttpContext.Current.Server.MapPath("~/App_Data/words_alpha_5.txt"));
        }

        public static string GetTodaysWord() {
            FileInfo wordleFile = new FileInfo(System.Web.HttpContext.Current.Server.MapPath("~/App_Data/wordle.txt"));

            if (!wordleFile.Exists || wordleFile.LastWriteTime.Date != DateTime.Now.Date) {
                // pick a new word
                File.WriteAllText(wordleFile.FullName, GetRandomWord());
            }

            return File.ReadAllText(wordleFile.FullName);
        }

        /// <summary> Return a random five letter word from the db. </summary>
        public static string GetRandomWord() {
            Random rnd = new Random();
            var allWords = GetAllFiveLetterWords();

            string word = "";
            int iter = 0;
            do {
                word = allWords[rnd.Next(allWords.Count)];
            } while (!IsValidWord(word) && iter++ < 1000);

            if (iter >= 1000) throw new Exception("Gave up after 1000 tries!"); // this should never happen, but we don't want it to get stuck in an inifite loop if it does

            return word;
        }

        /// <summary> Return all five letter words from database (distinct, uppercase, sorted). </summary>
        public static List<string> GetAllFiveLetterWords() {
            DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();

            IQueryable<DB.Message> allPublicMessages = dc.Messages.Where(o => o.roomId != SystemConfig.MailRoomId && !o.Room.@private);
            string allText = string.Join(" ", allPublicMessages.Select(o => o.body));

            MatchCollection mc = Regex.Matches(allText, "([a-zA-Z]+)");

            List<Match> mlist = mc.OfType<Match>().ToList();

            return mlist.Where(o => o.Value.Length == 5).Select(o => o.Value.ToUpper()).Distinct().OrderBy(o => o).ToList();
        }

        public static bool IsValidWord(string word) {
            if (word.Length != 5) return false;

            // slow perhaps? maybe cache this if it is
            string[] validWords = File.ReadAllLines(ValidWordsFile.FullName); // this word list is lower-case
            return (validWords.Contains(word.ToLower()));
        }
    }
}
