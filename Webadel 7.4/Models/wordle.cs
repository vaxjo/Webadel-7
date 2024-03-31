using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

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
            List<string> allWords = GetAllFiveLetterWords();

            //File.WriteAllText("E:\\Repos_Jarrin\\Webadel 7.4\\allwords.txt", string.Join("\r\n", allWords));

            return allWords[rnd.Next(allWords.Count)];
        }

        /// <summary> Return all valid five letter words from database (distinct, lowercase) in random order. </summary>
        public static List<string> GetAllFiveLetterWords() {
            //return new List<string> { "THING", "SEVEN", "ROAST", "BORES", "FIGHT", "STORM", "LICKS" };

            var all = (List<string>)Myriads.Cache.Get("Wordle", "GetAllFiveLetterWords", delegate () {
                DB.WebadelDataContext dc = DB.WebadelDataContext.GetProfiledDC();

                IQueryable<DB.Message> allPublicMessages = dc.Messages.Where(o => o.roomId != SystemConfig.MailRoomId && !o.Room.@private);
                string allText = " " + string.Join(" ", allPublicMessages.Select(o => o.body)) + " ";

                //File.WriteAllText("E:\\Repos_Jarrin\\Webadel 7.4\\allText.txt", allText);

                // all 5-letter strings; might not be valid words
                var allFiveLetterWords = Regex.Matches(allText, "\\W([a-zA-Z]{5})\\W").OfType<Match>().Select(o => o.Groups[1].Value.ToLower()).Distinct();

                string[] validWords = File.ReadAllLines(ValidWordsFile.FullName); // this word list is lower-case

                return validWords.Intersect(allFiveLetterWords).ToList();
            }, TimeSpan.FromMinutes(15));

            //File.WriteAllText("E:\\Repos_Jarrin\\Webadel 7.4\\edswords.txt", string.Join("\r\n", mlist.Select(o => o.Groups[1].Value).Distinct()));

            Random random = new Random();
            return all.OrderBy(o => random.Next()).ToList();
        }

        public static bool IsValidWord(string word) {
            if (word.Length != 5) return false;

            // slow perhaps? maybe cache this if it is
            string[] validWords = File.ReadAllLines(ValidWordsFile.FullName); // this word list is lower-case
            return (validWords.Contains(word.ToLower()));
        }

        public class Guess {
            public string Word; // "SWORD"

            public Guess(string word) {
                if (word.Trim().Length != 5) throw new Exception("Word must be 5 letters.");

                Word = word.ToUpper().Trim();
            }

            public Result Check() => Check(GetTodaysWord());

            public Result CheckMaladjusted() {
                // TODO: clear at midnight
                List<KeyValuePair<Guess, Result>> previous = (List<KeyValuePair<Guess, Result>>)HttpContext.Current.Session["wordle guesses"] ?? new List<KeyValuePair<Guess, Result>>();
                string potentialSolutionWord = "";

                // iterate db word list and find the first word that satisfies all of the eu's prev guesses 
                foreach (string word in GetAllFiveLetterWords()) {
                    if (!CheckMaladjustedPrev(word)) continue;

                    // found potential word! make sure 
                    potentialSolutionWord = word;

                    // check potential word against guess; if not correct then this is the solution word for the moment;
                    // if correct, keep looking (if can't find another, then this is the soluition)
                    var potentialResult = Check(potentialSolutionWord);
                    if (!potentialResult.IsCorrect) {
                        previous.Add(new KeyValuePair<Guess, Result>(this, potentialResult)); // add current guess and result
                        HttpContext.Current.Session["wordle guesses"] = previous;

                        return potentialResult;
                    }
                }

                // couldn't find a better match, so the eu has found it
                HttpContext.Current.Session["wordle guesses"] = null;

                return Check(potentialSolutionWord);
            }

            // checks if the specified word matches the player's history
            public bool CheckMaladjustedPrev(string word) {
                List<KeyValuePair<Guess, Result>> previous = (List<KeyValuePair<Guess, Result>>)HttpContext.Current.Session["wordle guesses"] ?? new List<KeyValuePair<Guess, Result>>();

                foreach (KeyValuePair<Guess, Result> prev in previous) {
                    if (prev.Key.Check(word).Encode != prev.Value.Encode) return false; 
                }

                return true;
            }

            public Result Check(string goalWord) {
                goalWord = goalWord.ToUpper().Trim();
                if (goalWord.Length != Word.Length) throw new Exception("Goal word is not the correct length.");

                Result result = new Result();

                // populate guesses with test word letters, all incorrect by default
                result.Guesses = Word.ToList().Select(o => new LetterGuess { Letter = o, State = LetterGuess.States.Incorrect }).ToList();

                // first, find all correct letters (incorrect letters from the goal word go into a list of leftovers)
                int i = 0;
                List<char> leftovers = new List<char>();
                foreach (LetterGuess lg in result.Guesses) {
                    if (lg.Letter == goalWord[i]) lg.State = LetterGuess.States.Correct;
                    else leftovers.Add(goalWord[i]);

                    i++;
                }

                // then look at the non-correct letters again
                foreach (LetterGuess lg in result.Guesses.Where(o => o.State != LetterGuess.States.Correct)) {
                    // if the letter is in the list of leftovers, then it's just in the wrong place; also remove the leftover letter from its list
                    if (leftovers.Contains(lg.Letter)) {
                        lg.State = LetterGuess.States.WrongPlace;
                        leftovers.Remove(lg.Letter);
                    }
                }

                return result;
            }

            public class LetterGuess {
                public enum States { Correct, Incorrect, WrongPlace }

                public char Letter;
                public States State;
            }

            public class Result {
                public List<LetterGuess> Guesses;

                public bool IsCorrect => Guesses.All(o => o.State == LetterGuess.States.Correct);

                public string Encode => string.Join(", ", Guesses.Select(o => o.State.ToString()));

                public Result() {
                    Guesses = new List<LetterGuess>();
                }
            }
        }
    }
}
