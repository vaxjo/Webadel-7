using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Webadel7 {
    public class Predictor {
        private List<List<string>> Corpus; // a list of string arrays, each string array is probably a message's worth of text

        public static List<string> GetWords(Guid userId, string word) {
            word = RemovePunctuation(word).Trim().ToLower();

            Predictor predict = (Predictor)Myriads.Cache.Get("Predictor", userId, delegate () {
                Predictor p = new Predictor { Corpus = new List<List<string>>() };

                DB.WebadelDataContext dc = new DB.WebadelDataContext();
                var allmessages = dc.Messages.Where(o => o.authorId == userId).Select(o => o.body);

                foreach (string message in allmessages) {
                    p.Corpus.Add(Regex.Split(RemovePunctuation(message).ToLower(), @"\s").Where(o => !string.IsNullOrWhiteSpace(o)).ToList());
                }

                return p;
            }, TimeSpan.FromMinutes(20));

            // look through the corpus and find the words that follow the given word
            Dictionary<string, int> followers = new Dictionary<string, int>(); // list of words that follow the given word, and their frequency

            foreach (List<string> message in predict.Corpus) {
                for (int i = 0; i < message.Count() - 1; i++) {
                    if (message[i] == word) {
                        string follower = message[i + 1];
                        if (followers.ContainsKey(follower)) followers[follower]++;
                        else followers.Add(follower, 1);
                    }
                }
            }

            // add some randomness so we don't get the same set of 3 each time
            Random rnd = new Random();
            foreach (string key in followers.Keys.ToList()) {
                followers[key] += rnd.Next(-1, 2);
            }

            // return the most common 3 (if any)
            var mostfreq = followers.OrderByDescending(o => o.Value);
            return mostfreq.Take(3).Select(o => o.Key).ToList();
        }

        public static void ClearCache(Guid userId) {
            Myriads.Cache.Remove("Predictor", userId);
        }

        /// <summary> Remove non-word punctuation from a line (. , ") but not apos. </summary>
        public static string RemovePunctuation(string line) {
            line = Regex.Replace(line, @"[`~!@#$%^&*()_+-=]", " ");
            line = Regex.Replace(line, @"[\\[\\]\\{}|]", " ");
            line = Regex.Replace(line, @"[;:""]", " ");
            line = Regex.Replace(line, @"[,./<>?]", " ");
            return line;
        }
    }
}

