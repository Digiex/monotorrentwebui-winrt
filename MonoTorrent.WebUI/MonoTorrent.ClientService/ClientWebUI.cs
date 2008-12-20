using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.ServiceProcess;
using System.Collections.Generic;
using MonoTorrent.Common;
using JSONSharp;
using JSONSharp.Values;
using JSONSharp.Collections;
using JSONPair = System.Collections.Generic.KeyValuePair<JSONSharp.Values.JSONStringValue, JSONSharp.JSONValue>;
using MonoTorrent.Client;
using System.Configuration;
using System.Xml;

namespace MonoTorrent.ClientService
{
    partial class ClientWebUI : ServiceBase
    {
        public ClientWebUI()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            stopFlag = false;
            stopHandle.Reset();
            listener.Start();
            listener.BeginGetContext(HandleRequest, listener);
        }

        protected override void OnStop()
        {
            stopFlag = true;
            listener.Abort();
            stopHandle.WaitOne();
        }

        #region Debug methods
        public void StartService()
        {
            OnStart(new string[] { });
        }

        public void StopService()
        {
            OnStop();
        }
        #endregion

        HttpListener listener;
        bool stopFlag = false;
        AutoResetEvent stopHandle = new AutoResetEvent(true);

        MonoTorrentClient<string> service;

        DirectoryInfo webui;

        public ClientWebUI(DirectoryInfo filesWebUI, int port, MonoTorrentClient<string> monoTorrentService)
        {
            InitializeComponent();

            listener = new HttpListener();
            listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;

            listener.Prefixes.Add(String.Format("http://*:{0}/gui/", port));

            service = monoTorrentService;
            webui = filesWebUI;
        }

        private void HandleRequest(IAsyncResult ar)
        {
            HttpListener http = (HttpListener)ar.AsyncState;

            try
            {
                Console.WriteLine("Waiting for request...");
                HttpListenerContext context = http.EndGetContext(ar);
                Console.WriteLine("Got request: " + context.Request.RawUrl);

                if (!context.Request.RawUrl.StartsWith("/gui/"))
                    WriteMessage(context, "invalid request");
                else if (context.Request.QueryString.Count == 0)
                    ProcessFile(context);
                else
                    ProcessRequest(context);

                context.Response.Close();
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }

            if (!stopFlag)
                http.BeginGetContext(HandleRequest, http);
            else
                stopHandle.Set();
        }

        public void WriteMessage(HttpListenerContext context, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);

            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.OutputStream.Write(data, 0, data.Length);
        }

        public void ProcessFile(HttpListenerContext context)
        {
            string fileName = context.Request.RawUrl.Substring(5);

            if (String.IsNullOrEmpty(fileName))
                fileName = "index.html";

            string filePath = Path.Combine(webui.FullName, fileName);

            if (String.CompareOrdinal(fileName, "index.html") == 0)
                ProcessIndexFile(context, filePath);
            else
            {
                using (FileStream data = File.OpenRead(filePath))
                {
                    byte[] buffer = new byte[1024];
                    int count;

                    while((count = data.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        context.Response.OutputStream.Write(buffer, 0, count);
                    }
                }
            }
        }

        private static void ProcessIndexFile(HttpListenerContext context, string filePath)
        {
            using (FileStream data = File.OpenRead(filePath))
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(data);
                XmlNode node = doc.SelectSingleNode("//*[@id='token']");

                if (node != null)
                    node.InnerText = Guid.NewGuid().ToString();

                context.Response.ContentType = "text/html";
                using (XmlWriter writer = new XmlTextWriter(context.Response.OutputStream, Encoding.UTF8))
                {
                    doc.WriteTo(writer);
                }
            }
        }

        public void ProcessRequest(HttpListenerContext context)
        {
            HttpListenerRequest Request = context.Request;
            HttpListenerResponse Response = context.Response;

            List<JSONValue> errors = new List<JSONValue>();

            JSONObjectCollection jsonResponse = new JSONObjectCollection();
            jsonResponse.Add(new JSONStringValue("build"), new JSONNumberValue(-1));

            context.Response.ContentType = "text/plain";

            string token = Request.QueryString["token"];
            string action = Request.QueryString["action"];
            string hash = Request.QueryString["hash"];
            bool list = Request.QueryString["list"] == "1";

            errors.Add(new JSONStringValue("Test message!"));

            try
            {
                switch (action)
                {
                    case "getfiles":
                        jsonResponse.AddRange(GetFiles(hash));
                        break;
                    case "getsettings":
                        string settingsFile = Path.Combine(webui.FullName, "StaticSettings.txt");
                        
                        List<JSONValue> settings = new List<JSONValue>(GetSettings(settingsFile));

                        jsonResponse.Add(new JSONStringValue("settings"), new JSONArrayCollection(settings));
                        jsonResponse.AddRange(List());
                        break;
                    case "pause":
                        jsonResponse.AddRange(Pause(hash));
                        break;
                    case "stop":
                        jsonResponse.AddRange(Stop(hash));
                        break;
                    case "start":
                        jsonResponse.AddRange(Start(hash));
                        break;
                    case "remove":
                        jsonResponse.AddRange(Remove(hash, false));
                        break;
                    case "removedata":
                        jsonResponse.AddRange(Remove(hash, true));
                        break;
                    case "setprio":
                        string fileIndexes = Request.QueryString["f"];
                        int priority = Int32.Parse(Request.QueryString["p"]);
                        jsonResponse.AddRange(SetPriority(hash, fileIndexes, priority));
                        break;
                    case "setprops":
                        string property = Request.QueryString["s"];
                        string value = Request.QueryString["v"];

                        if (property == "label")
                            jsonResponse.AddRange(SetLabel(hash, value));
                        break;
                    case "add-file":
                        //HttpPostedFile metaFile = Request.Files[0];

                        //if (metaFile.ContentLength > 524288) // 512KB
                        //{
                        //    errors.Add(new JSONStringValue("Torrent rejected: meta-data file is too large."));
                        //    break;
                        //}

                        //byte[] metaData = new byte[metaFile.ContentLength];

                        //int dataRead = 0;

                        //while (dataRead < metaFile.ContentLength)
                        //    dataRead += metaFile.InputStream.Read(metaData, dataRead, (metaData.Length - dataRead));

                        //AddFile(metaData);
                        errors.Add(new JSONStringValue("Uploading files not supported."));
                        break;
                    case "add-url":
                        string url = Request.QueryString["s"];
                        System.Net.WebClient web = new System.Net.WebClient();
                        byte[] data = web.DownloadData(url);

                        AddFile(data);
                        break;
                    case "stopall":
                        StopAll();
                        errors.Add(new JSONStringValue("All torrents stopped."));
                        break;

                    default:
                        errors.Add(new JSONStringValue("Unknown request. URL: " + Request.RawUrl));
                        break;
                }

                if (list)
                    jsonResponse.AddRange(List());

                if (errors.Count > 0)
                    jsonResponse.Add(new JSONStringValue("messages"), new JSONArrayCollection(errors));
            }
            catch (Exception e)
            {
                errors.Add(new JSONStringValue("Server Error: " + e.Message));
            }

            using (StreamWriter writer = new StreamWriter(Response.OutputStream))
            {
                writer.Write(jsonResponse.PrettyPrint());
            }
        }

        public IEnumerable<JSONValue> GetSettings(string settingsFile)
        {
            using (StreamReader sr = new StreamReader(settingsFile))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    JSONArrayCollection setting = new JSONArrayCollection();

                    string[] parts = line.Split(", ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                    foreach (string part in parts)
                    {
                        int numValue;
                        if (part.StartsWith("\"") && part.EndsWith("\""))
                            setting.Add(new JSONStringValue(part.Substring(1, part.Length - 2)));
                        else if (int.TryParse(part, out numValue))
                            setting.Add(new JSONNumberValue(numValue));
                    }

                    yield return setting;
                }
            }
        }

        private JSONArrayCollection GetLabels()
        {
            Dictionary<string, int> counts = new Dictionary<string, int>();
            //foreach (KeyValuePair<TorrentManager, string> pair in labels)
            //{
            //    if (!counts.ContainsKey(pair.Value))
            //    {
            //        counts[pair.Value] = 1;
            //    }
            //    else
            //    {
            //        counts[pair.Value]++;
            //    }
            //}
            List<JSONValue> response = new List<JSONValue>();
            foreach (KeyValuePair<string, int> pair in counts)
            {
                List<JSONValue> nameCountPair = new List<JSONValue>();
                nameCountPair.Add(new JSONStringValue(pair.Key));
                nameCountPair.Add(new JSONNumberValue(pair.Value));
                response.Add(new JSONArrayCollection(nameCountPair));
            }

            return new JSONArrayCollection(response);
        }

        public void AddFile(byte[] fileData)
        {
            service.AddTorrent(fileData, null, null,
                Settings.DefaultUploadSlots, Settings.DefaultMaxConnections,
                Settings.DefaultMaxDownloadSpeed, Settings.DefaultMaxUploadSpeed, false);
        }

        public IEnumerable<JSONPair> AddUrl(string url)
        {
            WebClient webClient = new WebClient();

            byte[] data = webClient.DownloadData(url);
            AddFile(data);

            yield break;
        }

        public IEnumerable<JSONPair> GetFiles(string hash)
        {
            Dictionary<JSONStringValue, JSONValue> jsonPairs = new Dictionary<JSONStringValue, JSONValue>();
            JSONStringValue emptyString = new JSONStringValue("");
            TorrentManager details = service.GetTorrentDetails(hash);

            List<JSONValue> hashFileInfoPair = new List<JSONValue>();
            List<JSONValue> fileList = new List<JSONValue>(details.FileManager.Files.Length);
            foreach (TorrentFile file in details.FileManager.Files)
            {
                List<JSONValue> fileInfo = new List<JSONValue>();
                fileInfo.Add(new JSONStringValue(file.Path));
                fileInfo.Add(new JSONNumberValue((double)file.Length));
                fileInfo.Add(new JSONNumberValue(0)); // TODO: What is this??? Progress?
                fileInfo.Add(new JSONNumberValue((int)PriorityAdapter(file.Priority)));
                fileList.Add(new JSONArrayCollection(fileInfo));
            }
            hashFileInfoPair.Add(new JSONStringValue(hash));
            hashFileInfoPair.Add(new JSONArrayCollection(fileList));

            yield return new JSONPair(new JSONStringValue("files"), new JSONArrayCollection(hashFileInfoPair));

            yield break;
        }

        private State StateAdapter(TorrentState state)
        {
            if (state == TorrentState.Paused)
            {
                return State.Paused;
            }
            else if ((state == TorrentState.Hashing))
            {
                return State.Queued;
            }
            else if ((state == TorrentState.Downloading) || (state == TorrentState.Seeding))
            {
                return State.Active;
            }
            else
            {
                return State.Stopped;
            }
        }

        private Priority PriorityAdapter(WebPriority priority)
        {
            if (priority == WebPriority.Skip)
            {
                return Priority.DoNotDownload;
            }
            else if (priority == WebPriority.Low)
            {
                return Priority.Low;
            }
            else if (priority == WebPriority.High)
            {
                return Priority.High;
            }
            else
            {
                return Priority.Normal;
            }
        }

        private WebPriority PriorityAdapter(Priority priority)
        {
            if (priority == Priority.DoNotDownload)
            {
                return WebPriority.Skip;
            }
            else if ((priority == Priority.Low) || (priority == Priority.Lowest))
            {
                return WebPriority.Low;
            }
            else if ((priority == Priority.High) || (priority == Priority.Highest) || (priority == Priority.Immediate))
            {
                return WebPriority.High;
            }
            else
            {
                return WebPriority.Normal;
            }
        }

        public IEnumerable<JSONPair> SetPriority(string hash, string fileIndexes, int webPriority)
        {
            Priority priority = PriorityAdapter((WebPriority)webPriority);
            string[] splitIndexes = fileIndexes.Split(new char[] { ',' });
            List<int> parsedIndexes = new List<int>(splitIndexes.Length);

            foreach (string fileIndex in splitIndexes)
            {
                try { parsedIndexes.Add(int.Parse(fileIndex)); }
                catch { continue; }
            }

            service.SetFilePriority(hash,
                parsedIndexes.ToArray(),
                priority);

            yield break;
        }

        public IEnumerable<JSONPair> List()
        {
            yield return new JSONPair(new JSONStringValue("label"), GetLabels());

            List<JSONValue> torrentList = new List<JSONValue>(service.Count);

            foreach (KeyValuePair<string, TorrentManager> mgr in service)
            {
                TorrentManager torrent = mgr.Value;
                List<JSONValue> torrentInfo = new List<JSONValue>();

                torrentInfo.Add(new JSONStringValue(mgr.Key)); /* Hash code */
                torrentInfo.Add(new JSONNumberValue((int)StateAdapter(torrent.State))); /* Status code */
                torrentInfo.Add(new JSONStringValue(torrent.Torrent.Name)); /* Name */
                torrentInfo.Add(new JSONNumberValue(torrent.Torrent.Size)); /* Total size */
                torrentInfo.Add(new JSONNumberValue(Math.Round(torrent.Progress, 2) * 10)); /* Percentage complete */
                torrentInfo.Add(new JSONNumberValue(torrent.Monitor.DataBytesDownloaded)); /* Downloaded bytes */
                torrentInfo.Add(new JSONNumberValue(torrent.Monitor.DataBytesUploaded)); /* Uploaded bytes */
                torrentInfo.Add(new JSONNumberValue((torrent.Monitor.DataBytesDownloaded + torrent.Monitor.ProtocolBytesDownloaded != 0) ? Math.Round(((float)torrent.Monitor.DataBytesUploaded + torrent.Monitor.ProtocolBytesUploaded) / (torrent.Monitor.DataBytesDownloaded + torrent.Monitor.ProtocolBytesDownloaded), 4) : 0)); /* Ratio */
                torrentInfo.Add(new JSONNumberValue(torrent.Monitor.UploadSpeed)); /* Upload speed (bytes) manager.Monitor.UploadSpeed */
                torrentInfo.Add(new JSONNumberValue(torrent.Monitor.DownloadSpeed)); /* Download speed (bytes) */
                torrentInfo.Add(new JSONNumberValue(torrent.Complete ? 0 : Math.Round((torrent.Torrent.Size - (torrent.Torrent.Size * (torrent.Progress / 100))) / (torrent.Monitor.DownloadSpeed * 1024), 0))); /* Remaining seconds */
                torrentInfo.Add(new JSONStringValue("")); /* Label */
                torrentInfo.Add(new JSONNumberValue(torrent.Peers.Leechs + torrent.Peers.Seeds)); /* Connected peers */
                torrentInfo.Add(new JSONNumberValue(torrent.Peers.Leechs + torrent.Peers.Seeds)); /* Total peers */
                torrentInfo.Add(new JSONNumberValue(torrent.Peers.Seeds)); /* Connected seeds */
                torrentInfo.Add(new JSONNumberValue(torrent.Peers.Leechs + torrent.Peers.Seeds)); /* Total seeds */
                torrentInfo.Add(new JSONNumberValue(1)); /* Availability? */
                torrentInfo.Add(new JSONNumberValue(torrent.Progress == 1.0 ? 1 : -1)); /* # */
                torrentInfo.Add(new JSONNumberValue(torrent.Torrent.Size - (torrent.Torrent.Size * torrent.Progress / 100))); /* Remaining bytes */
                //torrentInfo.Add(new JSONStringValue(manager.TrackerManager.CurrentTracker.State.ToString()));

                torrentList.Add(new JSONArrayCollection(torrentInfo));
            }

            yield return new JSONPair(new JSONStringValue("torrents"), new JSONArrayCollection(torrentList));
            yield return new JSONPair(new JSONStringValue("torrentc"), new JSONStringValue("0"));

            yield break;
        }

        public IEnumerable<JSONPair> Pause(string hashes)
        {
            foreach (string hash in hashes.Split(new char[] { ',' }))
            {
                service.Pause(hash);
            }
            return List();
        }

        public IEnumerable<JSONPair> Stop(string hashes)
        {
            foreach (string hash in hashes.Split(new char[] { ',' }))
            {
                service.Stop(hash);
            }
            return List();
        }

        public IEnumerable<JSONPair> Start(string hashes)
        {
            foreach (string hash in hashes.Split(new char[] { ',' }))
            {
                service.Start(hash);
            }
            return List();
        }

        public IEnumerable<JSONPair> Remove(string hashes, bool removeData)
        {
            string[] splitHashes = hashes.Split(new char[] { ',' });
            List<JSONValue> torrentmList = new List<JSONValue>(splitHashes.Length);
            foreach (string hash in splitHashes)
            {
                service.RemoveTorrent(hash, removeData);
                torrentmList.Add(new JSONStringValue(hash));
            }
            yield return new JSONPair(new JSONStringValue("torrentm"), new JSONArrayCollection(torrentmList));

            foreach (JSONPair pair in List())
            {
                yield return pair;
            }
        }

        public void StopAll()
        {
            service.StopAll();
        }

        public IEnumerable<JSONPair> SetLabel(string hash, string p)
        {
            yield break;
        }

        enum State
        {
            Active = 201,
            Stopped = 136,
            Queued = 200,
            Paused = 233
        }

        enum WebPriority
        {
            Skip = 0,
            Low = 1,
            Normal = 2,
            High = 3
        }
    }
}
