using System;
using System.Linq;
using System.Net;
using System.IO;
using System.Threading;
using System.Runtime.Serialization.Json;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using HtmlAgilityPack;

namespace HackerNews
{
	static class Constants 
	{
		public const string BASE = "https://hacker-news.firebaseio.com/v0/";
		public const string TOP = BASE + "topstories.json";
		public const string NEW = BASE + "newstories.json";
		public const string BEST = BASE + "beststories.json";
		public const string ITEM = BASE + "item/{0}.json";
	}

	class MainClass
	{
		//public static IndentedTextWriter writer = new IndentedTextWriter (Console.Out, "    ");

		public static List<string> getX(string x, int limit)
		{
			HttpWebRequest r = (HttpWebRequest) WebRequest.Create (x);
			r.Method = WebRequestMethods.Http.Get;
			r.Accept = "application/json";
			WebResponse resp = r.GetResponse ();
			DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<string>));
			List<string> output = (List<string>)serializer.ReadObject (resp.GetResponseStream ());
			return output.GetRange(0, Math.Min(limit, 50));
		}

		public static List<Tuple<string, Dictionary<string, dynamic>>> listX (List<string> ids, int limit = 50)
		{
			List<Thread> threads = new List<Thread>();
			List<Tuple<string, Dictionary<string, dynamic>>> r = new List<Tuple<string, Dictionary<string, dynamic>>>();
			foreach (string i in ids) {
				threads.Add (new Thread(() => addStory(i, r)));
				threads [threads.Count - 1].Start ();
			}
			bool running = true;
			while (running) {
				running = false;
				foreach (Thread t in threads) {
					if (t.IsAlive) {
						running = true;
						break;
					}
				}
				Thread.Sleep (20);
			}

			return r.GetRange(0, Math.Min(limit, 50));
		}

		public static List<string> getTop(int limit = 50)
		{
			return getX (Constants.TOP, limit);
		}

		public static List<string> getNew (int limit = 50) //Gets the IDs of the new stories
		{
			return getX (Constants.NEW, limit);
		}

		public static List<string> getBest (int limit = 50) //Gets the IDs of the best stories
		{
			return getX (Constants.BEST, limit);
		}

		public static Dictionary<string,dynamic> getItem(string id)
		{
			string text;
			JavaScriptSerializer jss = new JavaScriptSerializer ();

			HttpWebRequest r = (HttpWebRequest)WebRequest.Create (String.Format (Constants.ITEM, id));
			r.Method = WebRequestMethods.Http.Get;
			r.Accept = "application/json";
			WebResponse resp = r.GetResponse ();
			using (var sr = new StreamReader (resp.GetResponseStream ())) {
				text = sr.ReadToEnd ();
			}

			return jss.Deserialize<Dictionary<string, dynamic>> (text);
		}

		public static void addStory (string id, List<Tuple<string, Dictionary<string, dynamic>>> list)
		{
			Dictionary<string, dynamic> item = getItem (id);
			string title = ((item.ContainsKey ("title")) ? item ["title"] : item ["url"]) + " - by " + item ["by"];
			//Console.WriteLine (title);
			list.Add (new Tuple<string, Dictionary<string, dynamic>> (title, item));
		}

		public static void printStories(List<Tuple<string, Dictionary<string, dynamic>>> stories, int selected = 0) {
			Console.BackgroundColor = ConsoleColor.Black;
			Console.ForegroundColor = ConsoleColor.White;
			Console.Clear ();
			Console.WriteLine ("Stories:");
			foreach (int i in Enumerable.Range(0, stories.Count)) {
				if (i == selected) {
					Console.BackgroundColor = ConsoleColor.White;
					Console.ForegroundColor = ConsoleColor.Black;
					Console.WriteLine (stories [i].Item1);
					Console.BackgroundColor = ConsoleColor.Black;
					Console.ForegroundColor = ConsoleColor.White;
				} else {
					Console.WriteLine (stories [i].Item1);
				}
			}

		}

		public static void selectStory (List<Tuple<string, Dictionary<string, dynamic>>> stories)
		{
			printStories (stories);
			int selected = 0;

			ConsoleKey k = ConsoleKey.Spacebar;
			while (k != ConsoleKey.Enter) {
				k = Console.ReadKey ().Key;
				if (k == ConsoleKey.DownArrow) {
					selected = Math.Min (selected + 1, stories.Count - 1);
					printStories (stories, selected);
				} else if (k == ConsoleKey.UpArrow) {
					selected = Math.Max (selected - 1, 0);
					printStories (stories, selected);
				}
			}
			HtmlToText h = new HtmlToText ();
			Dictionary<string, dynamic> selStory = stories[selected].Item2;
			string story = selStory["title"] + "\n";
			try {
				story += h.ConvertHtml(selStory["text"]);
			} catch {
				story += selStory ["url"];
			}
			Console.Clear ();
			Console.WriteLine (story + " - by " + selStory ["by"] + "\n");
			Console.WriteLine ("Loading comments...");
			List<Dictionary<string, dynamic>> comments = getKids (selStory ["id"].ToString());
			Console.WriteLine (comments.Count.ToString() + " comments.\n");
			printComments (comments);
            Console.Read();
		}

		public static List<Dictionary<string, dynamic>> getKids (string id, int limit = -1)
		{
			List<Dictionary<string, dynamic>> output = new List<Dictionary<string, dynamic>> ();
			List<Thread> threads = new List<Thread> ();
			Dictionary<string, dynamic> item = getItem (id);
			int kids = (item.ContainsKey("kids")) ? item ["kids"].Count : 0;
			if (kids == 0) {
				return output;
			} else {
				foreach (int i in Enumerable.Range(0,
					(limit == -1) ? kids : Math.Min(limit, kids))) {
					threads.Add (new Thread (() => output.Add (getItem (item ["kids"] [i].ToString ()))));
					threads [i].Start ();
				}
				bool running = true;
				while (running) {
					running = false;
					foreach (Thread t in threads) {
						if (t.IsAlive) {
							running = true;
							break;
						}
					}
					Thread.Sleep (20);
				}
				return output;
			}
		}

		public static void printComments(List<Dictionary<string, dynamic>> comments,int maxDepth = -1, int curDepth = 0) 
		{
			HtmlToText h = new HtmlToText ();
			foreach (Dictionary<string, dynamic> c in comments) {
				int kids;
				try {
					kids = c["kids"].Count;
				} catch {
					kids = 0;
				}
				if (!c.ContainsKey("deleted")) {
                    string author = indent(c["by"] + ":", curDepth);
                    author = author.Remove(author.Length - 1);
                    Console.WriteLine (author);
					string s = indent(h.ConvertHtml(c["text"]), curDepth);
					s = s.Remove (s.Length - 1);
					Console.WriteLine(s);
				} else {
                    Console.WriteLine(indent("[deleted]:", curDepth));
                    Console.WriteLine(indent("[deleted]", curDepth));
                }
				if (kids > 0 && (curDepth < maxDepth || maxDepth == -1)) {
					Console.WriteLine ();
					printComments (getKids(c["id"].ToString()), maxDepth, curDepth + 1);
				}
				Console.WriteLine ();
			}
		}

		public static string indent (string s, int n, int baseIndent = 4) {
			string r = "";
			if (s.Contains ('\n')) {
				foreach (string sub in s.Split ('\n')) {
					r += indent (sub, n, baseIndent);
				}
				return r;
			} else {
				int length = Console.BufferWidth - n * baseIndent;
				string ind = new String (' ', baseIndent * n);
				while (s != "" && s != ".") {
					int index = (s.Length <= length) ?
						s.Length - 1 : Math.Max(s.LastIndexOf (' ', length - 1), s.LastIndexOf('.', length - 1));
					r += ind + s.Substring (0, index + 1) + "\n";
					s = s.Remove (0, index + 1);
				}
				return r;
			}
		}

		public static void printCategories(List<string> cat, int selected = 0) 
		{
			Console.BackgroundColor = ConsoleColor.Black;
			Console.ForegroundColor = ConsoleColor.White;
			Console.Clear ();
			Console.WriteLine ("Categories:");
			foreach (int i in Enumerable.Range(0, 3)) {
				if (i == selected) {
					Console.BackgroundColor = ConsoleColor.White;
					Console.ForegroundColor = ConsoleColor.Black;
					Console.WriteLine (cat [i]);
					Console.BackgroundColor = ConsoleColor.Black;
					Console.ForegroundColor = ConsoleColor.White;
				} else {
					Console.WriteLine (cat [i]);
				}
			}
		}

		public static void selectCategory(int selected = 0)
		{
			List<string> categories = new List<string>();
			categories.Add ("Top");
			categories.Add ("New");
			categories.Add ("Best");
			printCategories (categories);

			ConsoleKey k = ConsoleKey.Spacebar;
			while (k != ConsoleKey.Enter) {
				k = Console.ReadKey ().Key;
				if (k == ConsoleKey.DownArrow) {
					selected = Math.Min (selected + 1, categories.Count - 1);
					printCategories (categories, selected);
				} else if (k == ConsoleKey.UpArrow) {
					selected = Math.Max (selected - 1, 0);
					printCategories (categories, selected);
				}
			}
			List<string> ids = new List<string>();
			switch (selected) {
			case 0:
				ids = getTop ();
				break;
			case 1:
				ids = getNew ();
				break;
			case 2:
				ids = getBest ();
				break;
			default:
				break;
			}
			Console.WriteLine ("Loading stories...");
			selectStory (listX(ids));
		}

		public static void Main (string[] args)
		{
			selectCategory ();
		}
	}

	public class HtmlToText
	{
		public HtmlToText()
		{
		}

		public string Convert(string path)
		{
			HtmlDocument doc = new HtmlDocument();
			doc.Load(path);

			StringWriter sw = new StringWriter();
			ConvertTo(doc.DocumentNode, sw);
			sw.Flush();
			return sw.ToString();
		}

		public string ConvertHtml(string html)
		{
			HtmlDocument doc = new HtmlDocument();
			doc.LoadHtml(html);

			StringWriter sw = new StringWriter();
			ConvertTo(doc.DocumentNode, sw);
			sw.Flush();
			return sw.ToString();
		}

		private void ConvertContentTo(HtmlNode node, TextWriter outText)
		{
			foreach(HtmlNode subnode in node.ChildNodes)
			{
				ConvertTo(subnode, outText);
			}
		}

		public void ConvertTo(HtmlNode node, TextWriter outText)
		{
			string html;
			switch(node.NodeType)
			{
			case HtmlNodeType.Comment:
				// don't output comments
				break;

			case HtmlNodeType.Document:
				ConvertContentTo(node, outText);
				break;

			case HtmlNodeType.Text:
				// script and style must not be output
				string parentName = node.ParentNode.Name;
				if ((parentName == "script") || (parentName == "style"))
					break;

				// get text
				html = ((HtmlTextNode)node).Text;

				// is it in fact a special closing node output as text?
				if (HtmlNode.IsOverlappedClosingElement(html))
					break;

				// check the text is meaningful and not a bunch of whitespaces
				if (html.Trim().Length > 0)
				{
					outText.Write(HtmlEntity.DeEntitize(html));
				}
				break;

			case HtmlNodeType.Element:
				switch(node.Name)
				{
				case "p":
					// treat paragraphs as crlf
					outText.Write("\r\n");
					break;
				}

				if (node.HasChildNodes)
				{
					ConvertContentTo(node, outText);
				}
				break;
			}
		}
	}
}
