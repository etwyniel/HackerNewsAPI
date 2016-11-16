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
		
		public static List<string> getTop (int limit = 50)
		{
			HttpWebRequest r = (HttpWebRequest) WebRequest.Create (Constants.TOP);
			r.Method = WebRequestMethods.Http.Get;
			r.Accept = "application/json";
			WebResponse resp = r.GetResponse ();
			DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<string>));
			List<string> output = (List<string>) serializer.ReadObject(resp.GetResponseStream());

			return output.GetRange(0, Math.Min(limit, 50));
		}

		public static List<string> getNew (int limit = 50) //Gets the IDs of the new stories
		{
			//HttpWebRequest r = (HttpWebRequest) WebRequest.Create (Constants.NEW);
			return getX (Constants.NEW, limit);
		}

		public static List<string> getBest (int limit = 50) //Gets the IDs of the best stories
		{
			HttpWebRequest r = (HttpWebRequest) WebRequest.Create (Constants.BEST);
			r.Method = WebRequestMethods.Http.Get;
			r.Accept = "application/json";
			WebResponse resp = r.GetResponse ();
			DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<string>));
			List<string> output = (List<string>)serializer.ReadObject (resp.GetResponseStream ());
			return output.GetRange(0, Math.Min(limit, 50));
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

		public static List<Dictionary<string, dynamic>> getKids (string id, int limit = -1)
		{
			List<Dictionary<string, dynamic>> output = new List<Dictionary<string, dynamic>>();
			List<Thread> threads = new List<Thread>();
			Dictionary<string, dynamic> item = getItem (id);
			int kids = item ["kids"].Count;
			if (kids == 0) {
				return output;
			} else {
				foreach (int i in Enumerable.Range(0,
					(limit == -1) ? kids : Math.Min(limit, kids)))
				{
					threads.Add(new Thread(() => output.Add(getItem(item["kids"][i].ToString()))));
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
					Thread.Sleep (5);
				}
				return output;
			}
		}

		public void addComment(List<Dictionary<string, dynamic>> list, string id)
		{
			list.Add (getItem (id));
		}

		public static void Main (string[] args)
		{
			test2 ();
			HtmlToText h = new HtmlToText ();
			List<Dictionary<string, dynamic>> item = getKids (getBest () [0]);
			Console.WriteLine (item.Count.ToString() + " comments.");
			Console.WriteLine ("Best story: " + h.ConvertHtml (item [0] ["text"]) + " - by " + item [0] ["by"] + "\n");// + 
				//((item[0]["descendants"] > 0) ? item[0]["kids"].Count.ToString() : "0") + " replies.");//+ item["url"]);
		}

		public static Tuple<string> test()
		{
			return new Tuple ("a", "b", "c");
		}

		public static void test2()
		{
			string a, b, c = test ();
			Console.WriteLine (a + b + c);
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
