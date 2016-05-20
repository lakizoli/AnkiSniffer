using AnkiSniffer.BLL;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
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

		#region Implementation
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

			return (from item in cards
					orderby item.Word
					select item).ToList ();
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
