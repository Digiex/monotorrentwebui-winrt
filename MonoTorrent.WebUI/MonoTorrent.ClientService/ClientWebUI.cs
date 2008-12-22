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
using System.Diagnostics;

namespace MonoTorrent.ClientService
{
    /// <summary>
    /// This service runs a webserver which is a nackend for WebUI.
    /// </summary>
    partial class ClientWebUI : ServiceBase
    {
        private const string urlBase = "/gui/";
        private const string urlBaseWithQuery = "/gui/?";
        private const string urlBaseNoSlash = "/gui";
        private const string urlBaseNoSlashQuery = "/gui?";
        private const string indexFile = "index.html";
        private const string tokenFile = "token.html";

        /// <summary>
        /// Mini webserver
        /// </summary>
        HttpListener listener;
        
        /// <summary>
        /// Service which is running the MonoTorrent engine
        /// </summary>
        MonoTorrentClient service;

        /// <summary>
        /// WebUI files found here
        /// </summary>
        DirectoryInfo webui;

        // Cache the index document as an XmlDocument because 
        // XmlDocument.Load() takes too long to process for each request
        private XmlDocument indexDocument;
        private XmlNode indexTokenNode;

        #region Validation Regexes
        private const string dnsNameRegex = @"([a-zA-Z0-9]+(.[a-zA-Z0-9]+)*[a-zA-Z0-9])";

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

            listenWorker = new Thread(ListenLoop);
        }

        #region Service Control
        protected override void OnStart(string[] args)
        {
            stopFlag = false;
            LoadIndexDocument();

            listener.Start();
            listenWorker.Start();
        }

        protected override void OnStop()
        {
            stopFlag = true;
            listener.Stop();
            
            // wait for request to finish
            if (!listenWorker.Join(5000))
            {
                // taking too long
                listener.Abort(); // drop all incoming request and shutdown
                listenWorker.Join();
            }
        }

        protected override void OnPause()
        {
            Monitor.Enter(requestProcessLock);
        }

        protected override void OnContinue()
        {
            Monitor.Exit(requestProcessLock);
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
        /// Thread which runs ListenLoop()
        /// </summary>
        Thread listenWorker;

        /// <summary>
        /// Set to true to signal the listner loop to stop
        /// </summary>
        bool stopFlag = false;

        /// <summary>
        /// Used to pause and unpause the listener thread
        /// </summary>
        object requestProcessLock = new object();

        /// <summary>
        /// Check stopFlag, listen for request, marshal request, repeat.
        /// </summary>
        private void ListenLoop()
        {
            while (!stopFlag)
            {
                HttpListenerContext context = null;

                try
                {
                    context = listener.GetContext();

                    lock (requestProcessLock)
                    {
                        Trace.WriteLine("Received request: " + context.Request.RawUrl);

                        MarshalRequest(context);
                    }
                }
                catch (ObjectDisposedException) { } // listener.Abort() was called
                catch (Exception ex)
                {
                    Trace.WriteLine("Error: " + ex.Message);
                }
                finally
                {
                    if (context != null)
                        context.Response.Close();
                } 
            }
        }

        /// <summary>
        /// Dispatches the request to the appropriate function based on the request URL
        /// </summary>
        private void MarshalRequest(HttpListenerContext context)
        {
            if (!context.Request.RawUrl.StartsWith(urlBase)) // is request for "/gui/..."
            {
                // maybe user forgot the trailing slash (i.e. "/gui" or "/gui?...")
                if (context.Request.RawUrl.StartsWith(urlBaseNoSlashQuery)
                 || context.Request.RawUrl.StartsWith(urlBaseNoSlash))
                {
                    string withMissingSlash;
                    int queryStart = context.Request.RawUrl.IndexOf('?');

                    if (queryStart > -1) // is there a query string?
                        withMissingSlash = context.Request.RawUrl.Insert(queryStart, "/");
                    else
                        withMissingSlash = context.Request.RawUrl + "/";

                    if (withMissingSlash.StartsWith(urlBase)) // point them to the right place
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.MovedPermanently;
                        context.Response.Redirect(withMissingSlash); 
                    }
                    else
                        ProcessInvalidRequest(context); // nope, can't fix it
                }
                else
                    ProcessInvalidRequest(context); // don't know what user is asking for
            }
            else if (context.Request.QueryString.Count > 0)
                ProcessQueryRequest(context); /* /gui/some/path?token=...&action=...&hash=... */
            else
                ProcessFile(context); /* /gui/some/file.ext */
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
            const string tokenTemplate = "<html><div id='token' style='display:none;'>{0}</div></html>";
            Guid token = Guid.NewGuid();

            byte[] tokenData = Encoding.UTF8.GetBytes(String.Format(tokenTemplate, token));

            context.Response.ContentType = "text/html";
            context.Response.ContentEncoding = Encoding.UTF8;

            context.Response.OutputStream.Write(tokenData, 0, tokenData.Length);
        }

        /// <summary>
        /// Returns the index file.
        /// </summary>
        private void ProcessIndexFileRequest(HttpListenerContext context, string filePath)
        {
            lock (indexDocument)
            {
                indexTokenNode.InnerText = Guid.NewGuid().ToString();

                context.Response.ContentType = "text/html";
                using (XmlWriter writer = new XmlTextWriter(context.Response.OutputStream, Encoding.UTF8))
                {
                    indexDocument.WriteTo(writer);
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

            if (!Request.RawUrl.StartsWith(urlBaseWithQuery)) // request is for a file + query string
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
                                                        
                            PrintSettings(jsonWriter, settingsFile);
                            break;
                        case "setsetting":
                            //string setting = Request.QueryString["s"];
                            //string value = Request.QueryString["v"];
                            break;
                        case "getprops":
                            PrintProperties(jsonWriter, hash);
                            break;
                        case "setprops":
                            string property = Request.QueryString["s"];
                            string value = Request.QueryString["v"];

                            if (property == "label")
                                SetLabel(hash, value);
                            break;
                        case "getfiles":
                            PrintFiles(jsonWriter, hash);
                            break;
                        case "setprio":
                            string fileIndexes = Request.QueryString["f"];
                            int priority = Int32.Parse(Request.QueryString["p"]);

                            SetFilePriority(hash, fileIndexes, priority);
                            break;
                        case "start":
                            StartTorrents(hash);
                            break;
                        case "forcestart":
                            StartTorrents(hash);
                            break;
                        case "stop":
                            StopTorrents(hash);
                            break;
                        case "pause":
                            PauseTorrents(hash);
                            break;
                        case "unpause":
                            StartTorrents(hash);
                            break;
                        case "remove":
                            RemoveTorrents(jsonWriter, hash, false);
                            break;
                        case "removedata":
                            RemoveTorrents(jsonWriter, hash, true);
                            break;
                        case "recheck":
                            break;
                        case "add-file":
                            Response.ContentType = "text/plain";
                            jsonWriter.WritePropertyName("error");
                            jsonWriter.WriteValue("Uploading torrent files currently not supported.");
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
                        PrintTorrentList(jsonWriter);
                }
                catch (Exception e)
                {
                    Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    jsonWriter.WritePropertyName("error");
                    jsonWriter.WriteValue("Server Error: " + e.Message);
                }

                jsonWriter.WriteEndObject();
            }
        }
        #endregion

        #region Adaptor methods
        /// <summary>
        /// Prints the array of settings.
        /// </summary>
        private void PrintSettings(JsonWriter writer, string settingsFile)
        {
            // TODO: Reads from a static file, make settings dynamic.

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

        /// <summary>
        /// Outputs the corresponding torrent's properties.
        /// </summary>
        private void PrintProperties(JsonWriter writer, string hash)
        {
            TorrentManager details = service.GetTorrentManager(hash);

            writer.WritePropertyName("props");
            writer.WriteStartArray();

            if (details != null)
            {
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
            }
            else
                writer.WriteNull();

            writer.WriteEndArray();
        }

        /// <summary>
        /// Outputs the corresponding torrent's files.
        /// </summary>
        private void PrintFiles(JsonWriter writer, string hash)
        {
            TorrentManager details = service.GetTorrentManager(hash);

            writer.WritePropertyName("files");
            writer.WriteStartArray();

            writer.WriteValue(hash);

            if (details != null)
            {
                writer.WriteStartArray();
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
            }
            else
                writer.WriteNull();

            writer.WriteEndArray();
        }

        /// <summary>
        /// Outputs the list of all labels.
        /// </summary>
        private void PrintLabels(JsonWriter writer)
        {
            // TODO: Implement labels
            Dictionary<string, int> labels = new Dictionary<string, int>();

            writer.WritePropertyName("label");
            writer.WriteStartArray();

            foreach (KeyValuePair<string, int> label in labels)
            {
                writer.WriteStartArray();

                writer.WriteValue(label.Key);   // Label
                writer.WriteValue(label.Value); // Torrent count

                writer.WriteEndArray();
            }

            writer.WriteEndArray();
        }

        /// <summary>
        /// Outputs the list of torrents and labels.
        /// </summary>
        /// <param name="writer"></param>
        private void PrintTorrentList(JsonWriter writer)
        {
            PrintLabels(writer);

            writer.WritePropertyName("torrents");
            writer.WriteStartArray();
            
            foreach(KeyValuePair<string, TorrentManager> pair in service.TorrentManagers)
            {
                string hash = pair.Key;
                TorrentManager torrent = pair.Value;

                writer.WriteStartArray();

                long remBytes = (long)Math.Round(torrent.Torrent.Size * (1.0 - torrent.Progress));
                long remSeconds = (long)Math.Round(
                    (double)remBytes / (double)torrent.Monitor.DownloadSpeed
                    );
                int ratio = (int)Math.Round(
                    (double)torrent.Monitor.DataBytesDownloaded * 10 / (double)torrent.Monitor.DataBytesUploaded
                    );
                int totalPeers = torrent.Peers.Leechs + torrent.Peers.Seeds;
                int progress = (int)Math.Round(torrent.Progress * 10);

                writer.WriteValue(hash);                                //HASH (string),
                writer.WriteValue((int)StateAdapter(torrent.State));    //STATUS (integer),
                writer.WriteValue(torrent.Torrent.Name);                //NAME (string),
                writer.WriteValue(torrent.Torrent.Size);                //SIZE (integer in bytes),
                writer.WriteValue(progress);                            //PERCENT PROGRESS (integer in 1/10 of a percent),
                writer.WriteValue(torrent.Monitor.DataBytesDownloaded); //DOWNLOADED (integer in bytes),
                writer.WriteValue(torrent.Monitor.DataBytesUploaded);   //UPLOADED (integer in bytes),
                writer.WriteValue(ratio);                               //RATIO (integer in 1/10 of a percent),
                writer.WriteValue(torrent.Monitor.UploadSpeed);         //UPLOAD SPEED (integer in bytes per second),
                writer.WriteValue(torrent.Monitor.DownloadSpeed);       //DOWNLOAD SPEED (integer in bytes per second),
                writer.WriteValue(remSeconds);                          //ETA (integer in seconds),
                writer.WriteValue("");                                  //LABEL (string),
                writer.WriteValue(totalPeers);                          //PEERS CONNECTED (integer),
                writer.WriteValue(totalPeers);                          //PEERS IN SWARM (integer),
                writer.WriteValue(torrent.Peers.Seeds);                 //SEEDS CONNECTED (integer),
                writer.WriteValue(totalPeers);                          //SEEDS IN SWARM (integer),
                writer.WriteValue(1);                                   //AVAILABILITY (integer in 1/65535ths),
                writer.WriteValue(torrent.Complete ? 1 : -1);           //TORRENT QUEUE ORDER (integer),
                writer.WriteValue(remBytes);                            //REMAINING (integer in bytes)

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

        /// <summary>
        /// Parses the torrent metadata in the stream and registers it with the engine.
        /// </summary>
        /// <param name="fileData"></param>
        private void AddFile(Stream fileData)
        {
            service.AddTorrent(fileData, null, null,
                Settings.DefaultUploadSlots, Settings.DefaultMaxConnections,
                Settings.DefaultMaxDownloadSpeed, Settings.DefaultMaxUploadSpeed, false);
        }

        /// <summary>
        /// Sets priority of files (specified by indexes) in the torrent (specified by hash).
        /// </summary>
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

        /// <summary>
        /// Starts torrents specified in the comma separated list of hashes.
        /// </summary>
        private void StartTorrents(string hashes)
        {
            string[] hashList = hashes.Split(new char[] { ',' });

            foreach (string hash in hashList)
            {
                service.StartTorrent(hash);
            }
        }

        /// <summary>
        /// Pauses torrents specified in the comma separated list of hashes.
        /// </summary>
        private void PauseTorrents(string hashes)
        {
            string [] hashList = hashes.Split(new char[] { ',' });

            foreach (string hash in hashList)
            {
                service.PauseTorrent(hash);
            }
        }

        /// <summary>
        /// Stops torrents specified in the comma separated list of hashes.
        /// </summary>
        private void StopTorrents(string hashes)
        {
            string[] hashList = hashes.Split(new char[] { ',' });

            foreach (string hash in hashList)
            {
                service.StopTorrent(hash);
            }
        }

        /// <summary>
        /// Removes torrents specified in the comma separated list of hashes.
        /// </summary>
        private void RemoveTorrents(JsonWriter writer, string hashes, bool removeData)
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

        /// <summary>
        /// Sets the label for the specified torrent.
        /// </summary>
        /// <param name="hash"></param>
        /// <param name="label"></param>
        public void SetLabel(string hash, string label)
        {
            // TODO: Implement this.
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Converts MonoTorrent's torrent state into WebUI state.
        /// </summary>
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

        /// <summary>
        /// Converts priority from WebUI to MonoTorrent
        /// </summary>
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

        /// <summary>
        /// Converts priority from MonoTorrent to WebUI
        /// </summary>
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

        /// <summary>
        /// Helper method to load the index.html into an XmlDocument. Done OnStart() because it's rather slow.
        /// </summary>
        private void LoadIndexDocument()
        {
            indexDocument = new XmlDocument();

            lock (indexDocument)
            {
                string indexDocumentPath = Path.Combine(webui.FullName, indexFile);
                using (FileStream data = File.OpenRead(indexDocumentPath))
                {
                    indexDocument.Load(data);
                    indexTokenNode = indexDocument.SelectSingleNode("//*[@id='token']");
                }
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
