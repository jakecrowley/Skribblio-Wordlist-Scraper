using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace SkribblioWordlistScraper
{
    class Program
    {
        public static List<string> wordlist;
        public static string currentWord = "";

        static void Main(string[] args)
        {
            if (File.Exists("wordlist.json"))
                wordlist = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText("wordlist.json"));
            else
                wordlist = new List<string>();

            Console.Title = $"Skribbl.io Wordlist Scraper | Current Wordlist Count: {Program.wordlist.Count}";

            SkribblioClient host = new SkribblioClient("Chris");
            SkribblioClient client = new SkribblioClient("Jaker", host.code);

            Thread.Sleep(1000);

            host.startGame();

            while (true)
            {
                string word = Console.ReadLine();
                client.guessWord(word);
            }
        }
    }

    class SkribblioClient
    {
        WebClient client = new WebClient();

        public WebSocket ws;
        public string code;

        public SkribblioClient(string name, string key = null)
        {
            //client.Proxy = new WebProxy("127.0.0.1:8888");
            string poll = client.DownloadString("https://server4.skribbl.io:5008/socket.io/?token=null&EIO=3&transport=polling&t=N53CnQS");
            string sid = JsonConvert.DeserializeObject<dynamic>(poll.Substring(poll.IndexOf('{'))).sid;

            if (key == null)
            {
                string json = "42[\"userData\",{\"name\":\"" + name + "\",\"code\":\"\",\"avatar\":[17,5,10,-1],\"join\":\"\",\"language\":\"English\",\"createPrivate\":true}]";
                client.UploadString($"https://server4.skribbl.io:5008/socket.io/?token=null&EIO=3&transport=polling&t=N53Cnd-&sid={sid}", $"{json.Length}:{json}");
                code = client.DownloadString($"https://server4.skribbl.io:5008/socket.io/?token=null&EIO=3&transport=polling&t=N53Cnd-&sid={sid}").Split(new string[] { "{\"key\":\"" }, StringSplitOptions.None)[1].Split('"')[0];
            }
            else
            {
                string json = "42[\"login\",{\"name\":\"" + name + "\",\"code\":\"\",\"avatar\":[4,19,9,-1],\"join\":\"" + key + "\",\"language\":\"English\",\"createPrivate\":false}]";
                client.UploadString($"https://server4.skribbl.io:5008/socket.io/?token=null&EIO=3&transport=polling&t=N53Cnd-&sid={sid}", $"{json.Length}:{json}");

                json = "42[\"userData\",{\"name\":\"" + name + "\",\"code\":\"\",\"avatar\":[4,19,9,-1],\"join\":\"" + key + "\",\"language\":\"English\",\"createPrivate\":false}]";
                client.UploadString($"https://server4.skribbl.io:5008/socket.io/?token=null&EIO=3&transport=polling&t=N53Cnd-&sid={sid}", $"{json.Length}:{json}");
            }

            ws = new WebSocket($"wss://server4.skribbl.io:5008/socket.io/?token=null&EIO=3&transport=websocket&sid={sid}");
            //ws.SetProxy("http://127.0.0.1:8888/", "", "");

            ws.OnMessage += (sender, e) =>
            {
                if(e.Data.IndexOf('[') >= 0)
                    Console.WriteLine($"[WS] {e.Data.Substring(e.Data.IndexOf('['))}");
                else
                    Console.WriteLine($"[WS] {e.Data}");

                if (e.Data.Contains("lobbyReveal"))
                {
                    string word = JsonConvert.DeserializeObject<dynamic>(e.Data.Substring(e.Data.IndexOf('[')))[1].word;
                    if (!Program.wordlist.Contains(word))
                    {
                        Program.wordlist.Add(word);
                        Console.WriteLine($"New word found: {word}");
                    }
                }
                else if (e.Data.Contains("lobbyChooseWord"))
                {
                    dynamic json = JsonConvert.DeserializeObject<dynamic>(e.Data.Substring(e.Data.IndexOf('[')))[1];
                    if (json.words != null)
                    {
                        chooseWord();
                        Program.currentWord = json.words[0];

                        foreach (string word in json.words)
                        {
                            if (!Program.wordlist.Contains(word))
                            {
                                Program.wordlist.Add(word);
                                Console.WriteLine($"New word found: " + word);
                            }
                            else
                            {
                                Console.WriteLine($"Existing word found: {word}");
                            }
                        }

                        File.WriteAllText("wordlist.json", JsonConvert.SerializeObject(Program.wordlist));
                        Console.Title = $"Skribbl.io Wordlist Scraper | Current Word List Count: {Program.wordlist.Count}";
                    }

                    _ = Task.Run(() =>
                    {
                        Thread.Sleep(500);
                        guessWord(Program.currentWord);
                    });
                }
                else if(e.Data.Contains("lobbyLobby"))
                {
                    if (key == null)
                        startGame();
                }
            };

            ws.Connect();
            ws.Send("2probe");
            ws.Send("5");

            new Thread(() => { while (true) { ws.Send("2"); Thread.Sleep(25000); } }).Start();
        }

        public void startGame()
        {
            Console.WriteLine($"Current Word List Length: {Program.wordlist.Count}");
            ws.Send("42[\"lobbySetRounds\",\"10\"]");
            ws.Send("42[\"lobbyGameStart\",\"\"]");
        }

        public void chooseWord()
        {
            ws.Send("42[\"lobbyChooseWord\",0]");
        }

        public void guessWord(string word)
        {
            ws.Send("42[\"chat\",\""+word+"\"]");
        }
    }
}
