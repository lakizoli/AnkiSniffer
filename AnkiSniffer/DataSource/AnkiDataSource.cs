using AnkiSniffer.BLL;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AnkiSniffer.DataSource {
	public static class AnkiDataSource {
		public static List<Card> LoadPackage (string path, FastZipEvents zipEvents) {
			//Extract package
			string destPath = ExtractPackage (path, zipEvents);

			//Query SQLite database
			string[] files = Directory.GetFiles (destPath);
			if (files == null || files.Length <= 0)
				return null;

			string ankiName = (from item in files
							   where Path.GetExtension (item).ToLower () == ".anki2"
							   select item).FirstOrDefault ();

			if (string.IsNullOrEmpty (ankiName))
				return null;

			string connectionString = string.Format (@"Data Source={0};Version=3;", ankiName);
			using (SQLiteConnection conn = new SQLiteConnection (connectionString)) {
				conn.Open ();
				return QueryCards (conn);
			}
		}

		public static List<Card> TurnLanguages (List<Card> input) {
			Dictionary<string, Card> result = new Dictionary<string, Card> ();

			foreach (Card card in input) {
				foreach (string word in card.Translate) {
					if (string.IsNullOrWhiteSpace (word))
						continue;

					if (result.ContainsKey (word)) {
						Card turnedCard = result[word];
						turnedCard.Translate.Add (card.Word);
						turnedCard.Examples.AddRange (from item in card.Examples
										where item.ToLower ().Contains (card.Word.ToLower ())
										select item);
					} else {
						List<string> translate = new List<string> ();
						translate.Add (card.Word);

						var examples = (from item in card.Examples
										where item.ToLower ().Contains (card.Word.ToLower ())
										select item).ToList ();

						result.Add (word, new Card () {
							Content = card.Content,
							Tags = card.Tags,
							Word = word,
							Translate = translate,
							Examples = examples
						});
					}
				}
			}

			return result.Values.ToList ();
		}

		public delegate void ProgressCallback (double percent);

		public static List<Card> FilterSztaki (List<Card> input, string toLang, ProgressCallback cb) {
			//Original full page url: http://szotar.sztaki.hu/angol-magyar-szotar/search?fromlang=eng&tolang=hun&searchWord=hajr%C3%A1&langcode=hu&u=0&langprefix=&searchMode=CONTENT_EXACT&viewMode=full&ignoreAccents=0&dict%5B%5D=eng-hun-sztaki-dict
			//Original ajax url: http://szotar.sztaki.hu/ajax/ac.php?url=%3FsearchWord%3Dhajr%25C3%25A1%26lang%3Deng%26toLang%3Dhun%26dict%3Deng-hun-sztaki-dict%26outLanguage%3Dhun%26labelHandling%3DINLINE_WITH_PROPERTY%26searchMode%3Dword_prefix%26resultFormat%3Dautocomplete_merged%26pageSize%3D50

			List<Card> result = new List<Card> ();
			int lastProgress = -1;
			for (int i = 0; i < input.Count; ++i) {
				Card card = input[i];

				//Check translation on sztaki
				string url = @"http://szotar.sztaki.hu/ajax/ac.php?url=";
				url += WebUtility.UrlEncode (string.Format (@"?searchWord={0}&lang=eng&toLang=hun&dict=eng-hun-sztaki-dict&outLanguage=hun&labelHandling=INLINE_WITH_PROPERTY&searchMode=word_prefix&pageSize=10",
					WebUtility.UrlEncode (card.Word)));

				HttpWebRequest req = WebRequest.CreateHttp (url);
				HttpWebResponse resp = (HttpWebResponse)req.GetResponse ();
				if (resp.StatusCode == HttpStatusCode.OK) {
					using (Stream stream = resp.GetResponseStream ())
					using (StreamReader sr = new StreamReader (stream)) {
						string body = sr.ReadToEnd ();
						dynamic json = JsonConvert.DeserializeObject (body);

						Card res = null;

						foreach (dynamic item in json.contents.result) {
							string itemContent = item.content.ToString ();
							if (itemContent.ToLower () == card.Word.ToLower ()) { //Add word to result if valid
								List<string> translate = GetSztakiTranslations (item.connections, toLang);
								if (translate.Count > 0) {
									if (res == null) {
										res = new Card () {
											Word = card.Word,
											Translate = translate,
											Examples = card.Examples
										};
									} else {
										res.Translate.AddRange (translate);
									}
								}
							}
						}

						if (res != null) {
							result.Add (res);
						}

						int progress = (int) ((double)(i+1) / (double)input.Count * 100.0 + 0.5);
						if (progress != lastProgress) {
							lastProgress = progress;
							cb.Invoke (progress);
						}
					}
				}
			}

			return result;
		}

		#region Implementation
		private static List<string> GetSztakiTranslations (dynamic connections, string toLang) {
			List<string> res = new List<string> ();
			foreach (dynamic item in connections) {
				foreach (dynamic trans in item.connected.connections) {
					if (trans.name == "translation" &&
						trans.connected.deleted != true &&
						trans.connected.sourceLabel != null &&
						trans.connected.sourceLabel.text == "SZTAKI" &&
						trans.connected.language == toLang) {
						res.Add (trans.connected.content.ToString ());
					}
				}
			}
			return res;
		}

		private static string ExtractPackage (string path, FastZipEvents zipEvents) {
			string destPath = Path.Combine (Path.GetDirectoryName (path), Path.GetFileNameWithoutExtension (path) + "_extract");
			if (!Directory.Exists (destPath))
				Directory.CreateDirectory (destPath);

			FastZip zip = new FastZip (zipEvents);
			zip.ExtractZip (path, destPath, FastZip.Overwrite.Prompt, ConfirmOverwriteUnZip, null, null, false);

			return destPath;
		}

		private static bool ConfirmOverwriteUnZip (string fileName) {
			return false;
		}

		private static List<Card> QueryCards (SQLiteConnection conn) {
			SQLiteCommand cmd = new SQLiteCommand ("select tags, flds, * from notes", conn);
			SQLiteDataReader reader = cmd.ExecuteReader ();

			List<Card> cards = new List<Card> ();
			while (reader.Read ()) {
				string tags = (string)reader["tags"];
				string content = (string)reader["flds"];

				string[] items = content.Split ('\u001f');
				string[] translateItems = items[3].Split (new string[] { "<br/>", "<br />" }, StringSplitOptions.RemoveEmptyEntries);

				List<string> translate = (from item in translateItems
										  select RemoveAllHTMLTags (item.Replace ("&nbsp;", " "))).ToList ();

				string[] examples = items[1].Replace ("&nbsp;", " ").Split (new string[] { "<br/>", "<br />" }, StringSplitOptions.RemoveEmptyEntries);

				cards.Add (new Card () {
					Tags = tags,
					Content = content,
					Word = items[0].Replace ("&nbsp;", " "),
					Translate = translate,
					Examples = (from example in examples
								select Decorate (RemoveAllHTMLTags (example))).ToList ()
				});
			}

			return cards;
		}

		private static string RemoveAllHTMLTags (string field) {
			StringBuilder res = new StringBuilder ();

			bool isInTag = false;
			foreach (char ch in field) {
				if (ch == '<') {
					isInTag = true;
				} else if (ch == '>') {
					isInTag = false;
					res.Append (' ');
				} else if (!isInTag) {
					res.Append (ch);
				}
			}

			return res.ToString ().Trim ();
		}

		private static string Decorate (string field) {
			StringBuilder res = new StringBuilder ();

			bool isTagPrefix = false;
			char lastCh = '\0';
			foreach (char ch in field) {
				if (ch == '{' && lastCh == '{') {
					isTagPrefix = true;
				} else if (ch == '}' && lastCh == '}') {
					res.Append ("</Bold>");
				} else if (isTagPrefix && ch == ':') {
					isTagPrefix = false;
					res.Append ("<Bold>");
				} else if (!isTagPrefix && ch != '{' && ch != '}' && ch != ':') {
					res.Append (ch);
				}

				lastCh = ch;
			}

			return res.ToString ().Trim ();
		}
		#endregion
	}
}
