using System;
using System.IO;
using System.Net;
using System.Xml;
using System.Text;
using System.Threading;
using System.Configuration;
using System.ServiceProcess;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MonoTorrent.Common;
using MonoTorrent.Client;
using Newtonsoft.Json;

namespace MonoTorrent.ClientService
{
    /// <summary>
    /// This service exposes a webserver to communicate with WebUI.
    /// </summary>
    partial class ClientWebUI : ServiceBase
    {
        HttpListener listener;
        bool stopFlag = false;
        AutoResetEvent stopHandle = new AutoResetEvent(true);

        MonoTorrentClient service;
        DirectoryInfo webui;

        private const string urlBase = "/gui/";
        private const string indexFile = "index.html";
        private const string tokenFile = "token.html";

        #region Validation Regexes
        private const string dnsNameRegex = @"([a-zA-Z0-9][a-zA-Z0-9\-.]+[a-zA-Z0-9])";

        private const string IPv4AddrRegex = "(" +
            @"([01]?\d\d|2[0-4]\d|25[0-5])\." +
            @"([01]?\d\d|2[0-4]\d|25[0-5])\." +
            @"([01]?\d\d|2[0-4]\d|25[0-5])\." +
            @"([01]?\d\d|2[0-4]\d|25[0-5])" +
            ")";

        // TODO: Fix IPv6 matching regex
        private const string IPv6AddrRegex = @"(" +
            @"[0-9a-zA-Z:]+" +
            ")";

        private static readonly Regex listenPrefixValidator = new Regex(
            @"^https?://((" + dnsNameRegex + "|" + IPv4AddrRegex + "|" + IPv6AddrRegex + ")|[+]|[*])(:[0-9]{1,5})?/gui/$",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant); 
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filesWebUI">Directory containing the WebUI files. There must be an "index.html" present.</param>
        /// <param name="listenPrefix">Prefix to register with HttpListener. It must be of the form "http://hostname:port/gui/", hostname may be a wildcard (* or +), port is optional.</param>
        /// <param name="monoTorrentService">An instance of the MonoTorrent client service.</param>
        public ClientWebUI(DirectoryInfo filesWebUI, string listenPrefix, MonoTorrentClient monoTorrentService)
        {
            #region Validate Arguments
            if (filesWebUI == null)
                throw new ArgumentNullException("filesWebUI");

            if (!filesWebUI.Exists)
                throw new ArgumentException("WebUI directory does not exist.", "filesWebUI");
            else if (filesWebUI.GetFiles("index.html").Length == 0)
                throw new ArgumentException("WebUI directory does not contain an index file.", "filesWebUI");

            if (listenPrefix == null)
                throw new ArgumentNullException("listenPrefix");

            if (!listenPrefixValidator.IsMatch(listenPrefix))
                throw new ArgumentException("Invalid listener prefix.", "listenPrefix");

            if (monoTorrentService == null)
                throw new ArgumentNullException("monoTorrentService"); 
            #endregion
            
            InitializeComponent();

            listener = new HttpListener();
            listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;

            listener.Prefixes.Add(listenPrefix.ToString());

            service = monoTorrentService;
            webui = filesWebUI;
        }

        #region Service Control
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

        protected override void OnPause()
        {
            base.OnPause();
        }

        protected override void OnContinue()
        {
            base.OnContinue();
        }

        #region Debug methods
        [System.Diagnostics.Conditional("DEBUG")]
        public void StartService()
        {
            OnStart(new string[] { });
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public void StopService()
        {
            OnStop();
        }
        #endregion 

        #endregion

        #region Request Processing
        /// <summary>
        /// Implements the listener "loop" and marshals requests as appropriate.
        /// </summary>
        private void HandleRequest(IAsyncResult ar)
        {
            HttpListener http = (HttpListener)ar.AsyncState;

            try
            {
                HttpListenerContext context = http.EndGetContext(ar);
                Console.WriteLine("Got request for: " + context.Request.RawUrl);

                if (!context.Request.RawUrl.StartsWith(urlBase))
                    ProcessInvalidRequest(context);
                else if (context.Request.QueryString.Count == 0) // /gui/some/file.ext
                    ProcessFile(context);
                else // /gui/some/path?token=...&action=...&hash=...
                    ProcessQueryRequest(context);
                
                context.Response.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }

            if (!stopFlag)
                http.BeginGetContext(HandleRequest, http);
            else
                stopHandle.Set();
        }
        
        /// <summary>
        /// Writes a message to the response.
        /// </summary>
        private void Respond(HttpListenerContext context, HttpStatusCode httpStatusCode, string message)
        {
            context.Response.StatusCode = (int)httpStatusCode;

            byte[] data = Encoding.UTF8.GetBytes(message);

            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.OutputStream.Write(data, 0, data.Length);
        }

        /// <summary>
        /// Responds to a request determined to be invalid.
        /// </summary>
        private void ProcessInvalidRequest(HttpListenerContext context)
        {
            Respond(context, HttpStatusCode.BadRequest, "invalid request");
        }

        /// <summary>
        /// Writes the file specified in the request URL into the response stream.
        /// </summary>
        private void ProcessFile(HttpListenerContext context)
        {
            string filePath = context.Request.RawUrl.Substring(urlBase.Length);
            string query = null;

            int queryStart = filePath.IndexOf('?');
            if (queryStart > -1)
            {
                query = filePath.Substring(queryStart);
                filePath = filePath.Substring(0, filePath.Length - queryStart);
            }

            if (String.IsNullOrEmpty(filePath))
                filePath = indexFile;

            filePath = Path.Combine(webui.FullName, filePath);
            string fileName = Path.GetFileName(filePath);

            if (String.CompareOrdinal(fileName, indexFile) == 0)
                ProcessIndexFileRequest(context, filePath);
            else if (String.CompareOrdinal(fileName, tokenFile) == 0)
                ProcessTokenRequest(context);
            else if (File.Exists(filePath))
            {
                using (FileStream data = File.OpenRead(filePath))
                {
                    byte[] buffer = new byte[1024];
                    int count;

                    while ((count = data.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        context.Response.OutputStream.Write(buffer, 0, count);
                    }
                }
            }
            else
                Respond(context, HttpStatusCode.NotFound, "404 File not found.");
        }

        /// <summary>
        /// Answers with a token number.
        /// </summary>
        private void ProcessTokenRequest(HttpListenerContext context)
        {
            byte[] tokenData = Guid.NewGuid().ToByteArray();

            context.Response.OutputStream.Write(tokenData, 0, tokenData.Length);
        }

        /// <summary>
        /// Returns the index file.
        /// </summary>
        private void ProcessIndexFileRequest(HttpListenerContext context, string filePath)
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

        /// <summary>
        /// Processes "http://host:port/gui/?..." requests, this is where the important logic is.
        /// </summary>
        private void ProcessQueryRequest(HttpListenerContext context)
        {
            HttpListenerRequest Request = context.Request;
            HttpListenerResponse Response = context.Response;
            
            if (!Request.RawUrl.StartsWith("/gui/?")) // request is for a file + query string
            {
                ProcessFile(context);
                return;
            }

            Response.ContentType = "application/json";
            Response.ContentEncoding = Encoding.UTF8;

            using (JsonWriter jsonWriter = new JsonWriter(new StreamWriter(Response.OutputStream, Response.ContentEncoding)))
            {
                string token = Request.QueryString["token"];
                string action = Request.QueryString["action"];
                string hash = Request.QueryString["hash"];
                bool list = String.CompareOrdinal(Request.QueryString["list"], "1") == 0;

                jsonWriter.WriteStartObject();

                jsonWriter.WritePropertyName("build");
                jsonWriter.WriteValue(-1);

                try
                {
                    switch (action)
                    {
                        case "getsettings":
                            string settingsFile = Path.Combine(webui.FullName, "StaticSettings.txt");
                                                        
                            WriteSettings(jsonWriter, settingsFile);
                            break;
                        case "setsetting":
                            //string setting = Request.QueryString["s"];
                            //string value = Request.QueryString["v"];
                            break;
                        case "getprops":
                            WriteProperties(jsonWriter, hash);
                            break;
                        case "setprops":
                            string property = Request.QueryString["s"];
                            string value = Request.QueryString["v"];

                            if (property == "label")
                                SetLabel(hash, value);
                            break;
                        case "getfiles":
                            WriteFiles(jsonWriter, hash);
                            break;
                        case "setprio":
                            string fileIndexes = Request.QueryString["f"];
                            int priority = Int32.Parse(Request.QueryString["p"]);

                            SetFilePriority(hash, fileIndexes, priority);
                            break;
                        case "start":
                            Start(hash);
                            break;
                        case "forcestart":
                            Start(hash);
                            break;
                        case "stop":
                            Stop(hash);
                            break;
                        case "pause":
                            Pause(hash);
                            break;
                        case "unpause":
                            Start(hash);
                            break;
                        case "remove":
                            Remove(jsonWriter, hash, false);
                            break;
                        case "removedata":
                            Remove(jsonWriter, hash, true);
                            break;
                        case "recheck":
                            break;
                        case "add-file":
                            Response.ContentType = "text/plain";
                            jsonWriter.WritePropertyName("error");
                            jsonWriter.WriteValue("Uploading files not currently supported.");
                            break;
                        case "add-url":
                            string url = Request.QueryString["s"];
                            System.Net.WebClient web = new System.Net.WebClient();
                            byte[] data = web.DownloadData(url);
                            
                            AddFile(new MemoryStream(data, 0, data.Length, false, false));
                            
                            break;

                        case null: 
                            break; // do nothing

                        default:
                            jsonWriter.WritePropertyName("error");
                            jsonWriter.WriteValue("Unknown request. URL: " + Request.RawUrl);
                            break;
                    }

                    if (list)
                        WriteTorrentList(jsonWriter);
                }
                catch (Exception e)
                {
                    jsonWriter.WritePropertyName("error");
                    jsonWriter.WriteValue("Server Error: " + e.Message);
                }

                jsonWriter.WriteEndObject();
            }
        }
        #endregion

        #region Adaptor methods
        private void WriteSettings(JsonWriter writer, string settingsFile)
        {
            writer.WritePropertyName("settings");
            writer.WriteStartArray();

            using (StreamReader sr = new StreamReader(settingsFile))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    string[] parts = line.Split(", ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

                    writer.WriteStartArray();

                    foreach (string part in parts)
                    {
                        int numValue;
                        if (part.StartsWith("\"") && part.EndsWith("\""))
                            writer.WriteValue(part.Substring(1, part.Length - 2));
                        else if (int.TryParse(part, out numValue))
                            writer.WriteValue(numValue);
                    }

                    writer.WriteEndArray();
                }
            }

            writer.WriteEndArray();
        }

        private void WriteProperties(JsonWriter writer, string hash)
        {
            writer.WritePropertyName("props");
            writer.WriteStartArray();
            TorrentManager details = service.GetTorrentDetails(hash);

            writer.WriteStartObject();

            writer.WritePropertyName("hash");           //HASH (string)
            writer.WriteValue(hash);
            writer.WritePropertyName("trackers");       //TRACKERS (string)
            writer.WriteValue("");
            writer.WritePropertyName("ulrate");         //UPLOAD LIMIT (integer in bytes per second)
            writer.WriteValue(details.Settings.MaxUploadSpeed);
            writer.WritePropertyName("dlrate");         //DOWNLOAD LIMIT (integer in bytes per second)
            writer.WriteValue(details.Settings.MaxDownloadSpeed);
            writer.WritePropertyName("superseed");      //INITIAL SEEDING (integer)
            writer.WriteValue((int)Option.Disabled);
            writer.WritePropertyName("dht");            //USE DHT (integer)
            writer.WriteValue((int)Option.Disabled);
            writer.WritePropertyName("pex");            //USE PEX (integer)
            writer.WriteValue((int)Option.Disabled);
            writer.WritePropertyName("seed_override");  //OVERRIDE QUEUEING (integer)
            writer.WriteValue((int)Option.Disabled);
            writer.WritePropertyName("seed_ratio");     //SEED RATIO (integer in 1/10 of a percent)
            writer.WriteValue(1000);
            writer.WritePropertyName("seed_time");      //SEEDING TIME (integer in seconds)
            writer.WriteValue(0);
            writer.WritePropertyName("ulslots");        //UPLOAD SLOTS (integer)
            writer.WriteValue(details.Settings.UploadSlots);

            writer.WriteEndObject();

            writer.WriteEndArray();
        }

        private void WriteFiles(JsonWriter writer, string hash)
        {
            writer.WritePropertyName("files");
            writer.WriteStartArray();

            writer.WriteValue(hash);

            writer.WriteStartArray();
            TorrentManager details = service.GetTorrentDetails(hash);
            
            foreach (TorrentFile file in details.FileManager.Files)
            {
                writer.WriteStartArray();

                writer.WriteValue(file.Path);   //FILE NAME (string)
                writer.WriteValue(file.Length); //FILE SIZE (integer in bytes)
                writer.WriteValue(0);           //DOWNLOADED (integer in bytes)
                writer.WriteValue((int)PriorityAdapter(file.Priority)); //PRIORITY* (integer)

                writer.WriteEndArray();
            }
            writer.WriteEndArray();

            writer.WriteEndArray();
        }

        private void WriteLabels(JsonWriter writer)
        {
            writer.WritePropertyName("label");
            writer.WriteStartArray();

            foreach (object setting in new object[] { })
            {
                writer.WriteStartArray();

                writer.WriteValue("Label Name"); // Label
                writer.WriteValue(-1);           // Torrent count

                writer.WriteEndArray();
            }

            writer.WriteEndArray();
        }

        private void WriteTorrentList(JsonWriter writer)
        {
            WriteLabels(writer);

            writer.WritePropertyName("torrents");
            writer.WriteStartArray();

            IEnumerator<KeyValuePair<string, TorrentManager>> torrentEnum = service.GetTorrentEnumerator();
            while (torrentEnum.MoveNext())
            {
                string hash = torrentEnum.Current.Key;
                TorrentManager torrent = torrentEnum.Current.Value;

                writer.WriteStartArray();

                writer.WriteValue(hash);                                //HASH (string),
                writer.WriteValue((int)StateAdapter(torrent.State));    //STATUS* (integer),
                writer.WriteValue(torrent.Torrent.Name);                //NAME (string),
                writer.WriteValue(torrent.Torrent.Size);                //SIZE (integer in bytes),
                writer.WriteValue(torrent.Progress * 100);              //PERCENT PROGRESS (integer in 1/10 of a percent),
                writer.WriteValue(torrent.Monitor.DataBytesDownloaded); //DOWNLOADED (integer in bytes),
                writer.WriteValue(torrent.Monitor.DataBytesUploaded);   //UPLOADED (integer in bytes),
                writer.WriteValue(0);                                   //RATIO (integer in 1/10 of a percent),
                writer.WriteValue(torrent.Monitor.UploadSpeed);         //UPLOAD SPEED (integer in bytes per second),
                writer.WriteValue(torrent.Monitor.DownloadSpeed);       //DOWNLOAD SPEED (integer in bytes per second),
                writer.WriteValue(0);                                   //ETA (integer in seconds),
                writer.WriteValue("");                                  //LABEL (string),
                writer.WriteValue(torrent.Peers.Leechs + torrent.Peers.Seeds);//PEERS CONNECTED (integer),
                writer.WriteValue(torrent.Peers.Leechs + torrent.Peers.Seeds);//PEERS IN SWARM (integer),
                writer.WriteValue(torrent.Peers.Seeds);                 //SEEDS CONNECTED (integer),
                writer.WriteValue(torrent.Peers.Leechs + torrent.Peers.Seeds);//SEEDS IN SWARM (integer),
                writer.WriteValue(1);                                   //AVAILABILITY (integer in 1/65535ths),
                writer.WriteValue(torrent.Complete ? 1 : -1);           //TORRENT QUEUE ORDER (integer),
                writer.WriteValue((long)Math.Round(torrent.Torrent.Size * (1.0 - torrent.Progress)));//REMAINING (integer in bytes)

                writer.WriteEndArray();
            }

            writer.WriteEndArray();

            // TODO: Implement caching mechanism
            //writer.WritePropertyName("torrentp");
            //writer.WriteStartArray();
            //writer.WriteEndArray();

            //writer.WritePropertyName("torrentm");
            //writer.WriteStartArray();
            //writer.WriteEndArray();

            //writer.WritePropertyName("torrentc");
            //writer.WriteValue(0);
        }

        private void AddFile(Stream fileData)
        {
            service.AddTorrent(fileData, null, null,
                Settings.DefaultUploadSlots, Settings.DefaultMaxConnections,
                Settings.DefaultMaxDownloadSpeed, Settings.DefaultMaxUploadSpeed, false);
        }

        private void SetFilePriority(string hash, string fileIndexes, int webPriority)
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
        }

        private void Pause(string hashes)
        {
            foreach (string hash in hashes.Split(new char[] { ',' }))
            {
                service.Pause(hash);
            }
        }

        private void Stop(string hashes)
        {
            foreach (string hash in hashes.Split(new char[] { ',' }))
            {
                service.Stop(hash);
            }
        }

        private void Start(string hashes)
        {
            foreach (string hash in hashes.Split(new char[] { ',' }))
            {
                service.Start(hash);
            }
        }

        private void Remove(JsonWriter writer, string hashes, bool removeData)
        {
            string[] splitHashes = hashes.Split(new char[] { ',' });

            writer.WritePropertyName("torrentm");
            writer.WriteStartArray();

            foreach (string hash in splitHashes)
            {
                service.RemoveTorrent(hash, removeData);
                writer.WriteValue(hash);
            }

            writer.WriteEndArray();

            //yield return new JSONPair(new JSONStringValue("torrentm"), new JSONArrayCollection(torrentmList));
        }

        public void SetLabel(string hash, string label)
        {
        }
        #endregion

        #region Helpers
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

        enum State : int
        {
            Active = 201,
            Stopped = 136,
            Queued = 200,
            Paused = 233
        }

        enum WebPriority : int
        {
            Skip = 0,
            Low = 1,
            Normal = 2,
            High = 3
        }

        enum Option : int
        {
            NotAllowed = -1,
            Disabled = 0,
            Enabled = 1
        }
        #endregion
    }
}
