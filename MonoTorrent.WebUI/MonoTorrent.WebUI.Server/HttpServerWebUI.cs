using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using MonoTorrent.Client;
using MonoTorrent.Common;
using MonoTorrent.WebUI.Common;
using MonoTorrent.WebUI.Server.Utility;
using Newtonsoft.Json;
using MonoTorrent.WebUI.Server.Configuration;

namespace MonoTorrent.WebUI.Server
{
	/// <summary>
	/// WebUI HTTP server implementation
	/// </summary>
    public class HttpServerWebUI : IDisposable
	{
        /// <summary>
        /// This is the webserver.
        /// </summary>
        private HttpListener httpListener;

        /// <summary>
        /// Maps configuration between WebUI and MonoTorrent.
        /// </summary>
        private SettingsAdapter settingsAdapter;
        
        /// <summary>
        /// Reference to the MonoTorrent engine.
        /// </summary>
        private ITorrentController torrents;

        /// <summary>
        /// Thread which runs ListenLoop()
        /// </summary>
        private Thread listenWorker;

        /// <summary>
        /// WebUI root directory.
        /// </summary>
        public DirectoryInfo DirWebUI
        {
            get;
            private set;
        }
        
        /// <summary>
        /// Build number reported to WebUI.
        /// </summary>
        public int BuildNumber
        {
            get;
            internal set;
        }

        /// <summary>
        /// Sets the encoding for HTTP responces.
        /// </summary>
        public Encoding ResponseEncoding
        {
            get;
            internal set;
        }

        /// <summary>
        /// Initializes a WebUI HTTP server.
        /// </summary>
        public HttpServerWebUI()
        {
            this.httpListener = new HttpListener();
            this.httpListener.AuthenticationSchemeSelectorDelegate = SelectAuthScheme;

            this.listenWorker = new Thread(ListenLoop);
            this.listenWorker.Name = "HTTP Listener Thread";

            BuildNumber = this.GetType().Assembly.GetName().Version.Build;
            ResponseEncoding = Encoding.UTF8;
        }

        #region State Control API
        /// <summary>
        /// Begin listening for HTTP requests.
        /// </summary>
        /// <param name="listenerPrefix">HTTP Listener prefix, see <see cref="System.Net.HttpListener"/>.</param>
        /// <param name="dirWebUI">Directory containing WebUI files.</param>
        /// <param name="settingsAdapter">SettingsAdapter instance.</param>
        public void StartHttpServer(ITorrentController torrents, WebUISection config)
        {
            if (torrents == null)
                throw new ArgumentNullException("torrents");

            if (config == null)
                throw new ArgumentNullException("config");

            if (httpListener.IsListening)
                throw new InvalidOperationException("Server is currently running.");

            this.DirWebUI = config.DirWebUI;
            this.settingsAdapter = new SettingsAdapter(config, torrents);
            this.torrents = torrents;

            httpListener.Prefixes.Clear();
            httpListener.Prefixes.Add(config.HttpListenerPrefix);

            stopFlag = false;
            LoadIndexDocument();

            httpListener.Start();
            listenWorker.Start();
        }

        /// <summary>
        /// Stop listening for HTTP requests.
        /// </summary>
        public void StopHttpServer()
        {
            if (!httpListener.IsListening)
                return;

            stopFlag = true;
            httpListener.Stop();

            // wait for request to finish processing
            if (!listenWorker.Join(5000))
            {
                // taking too long
                httpListener.Abort(); // drop all incoming request and shutdown
                listenWorker.Join();
            }

            this.settingsAdapter = null;
            this.torrents = null;
        }
        #endregion

        #region HTTP Authentication
        /// <summary>
        /// Determines the authentication method to use for this request.
        /// </summary>
        static AuthenticationSchemes SelectAuthScheme(HttpListenerRequest request)
        {
            // Do not authenticate local machine requests.
            if (IsLocal(request))
                return AuthenticationSchemes.Anonymous;
            else
                return AuthenticationSchemes.Negotiate;
        }

        /// <summary>
        /// Determines if the request came through a loopback interface.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        private static bool IsLocal(HttpListenerRequest request)
        {
            return request.LocalEndPoint.Address.Equals(request.RemoteEndPoint.Address);
        }
        #endregion

        #region HTTP Request Handling

        #region index.htm cache
        /// <summary>
        /// Load index.htm into memory when the server starts. (takes a while!)
        /// </summary>
        private XmlDocument indexDocument;

        /// <summary>
        /// Element in index.htm where the token is written.
        /// </summary>
        private XmlNode indexTokenNode;

        /// <summary>
        /// Loads index.html into memory to modify and return the token quickly.
        /// </summary>
        private void LoadIndexDocument()
        {
            indexDocument = new XmlDocument();

            lock (indexDocument)
            {
                string indexDocumentPath = Path.Combine(DirWebUI.FullName, IndexFile);
                using (FileStream data = File.OpenRead(indexDocumentPath))
                {
                    TraceWriteLine("Loading {0}...", IndexFile);
                    indexDocument.Load(data);
                    indexTokenNode = indexDocument.SelectSingleNode("//*[@id='token']");
                    TraceWriteLine("File {0} cached in memory.", IndexFile);
                }
            }
        }
        #endregion

        /// <summary>
        /// Set to true to signal the listner loop to halt.
        /// </summary>
        private bool stopFlag = false;

        /// <summary>
        /// Used to pause and unpause the httpListener thread
        /// </summary>
        private object requestProcessLock = new object();

        /// <summary>
        /// Check stopFlag, listen for request, marshal request, repeat.
        /// </summary>
        private void ListenLoop()
        {
            while (!stopFlag)
            {
                // MonoTorrent library is not thread-safe so we'll
                // serve requests serially.

                HttpListenerContext context = null;
                try
                {
                    lock (requestProcessLock)
                    {
                        TraceWriteLine("Waiting for HTTP request...");

                        context = httpListener.GetContext();

                        TraceHttpRequest(context);

                        MarshalRequest(context);

                        TraceHttpResponse(context);
                    }
                }
                catch (ObjectDisposedException) { } // httpListener.Abort() was called
                catch (HttpListenerException ex)
                {
                    if (ex.ErrorCode != 995) // Stop() was called on the listener.
                        throw;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
                }
                finally
                {
                    if (context != null)
                        context.Response.Close();
                }
            }
        }

        /// <summary>
        /// Dispatches the request to the appropriate handler based on the URL
        /// </summary>
        private void MarshalRequest(HttpListenerContext context)
        {
            if (RedirectToCanonicalUrl(context))
                return; // request redirected from /gui... to /gui/...

            if (Regex.IsMatch(context.Request.RawUrl, "^/gui/[?]"))
                ProcessQueryRequest(context); // /gui/some/path?token=...&action=...&hash=...
            else if (Regex.IsMatch(context.Request.RawUrl, "^/gui/"))
                ProcessFileRequest(context); // /gui/some/file.ext
            else
                ProcessInvalidRequest(context); // we don't serve that here
        }

        /// <summary>
        /// Writes a message as the response.
        /// </summary>
        private void Respond(HttpListenerContext context, HttpStatusCode httpStatusCode, string message)
        {
            context.Response.StatusCode = (int)httpStatusCode;

            byte[] data = Encoding.UTF8.GetBytes(message);

            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.Close(data, false);
        }

        /// <summary>
        /// Responds to a request determined to be invalid.
        /// </summary>
        private void ProcessInvalidRequest(HttpListenerContext context)
        {
            Respond(context, HttpStatusCode.BadRequest, "Invalid Request");
        }

        /// <summary>
        /// Checks for a missing trailing slash, redirects accordingly.
        /// </summary>
        private bool RedirectToCanonicalUrl(HttpListenerContext context)
        {
            if (Regex.IsMatch(context.Request.RawUrl, "^/gui([?].*)?$"))
            {
                // client requested /gui  or /gui?<query>
                // it should be     /gui/ or /gui/?<query>

                string fixedUrl = context.Request.RawUrl.Insert("/gui".Length, "/");

                context.Response.StatusCode = (int)HttpStatusCode.MovedPermanently;
                context.Response.Redirect(fixedUrl);

                return true;
            }
            else
                return false;
        }

        #region Constants
        private const string IndexFile = "index.html";
        private const string TokenFile = "token.html";
        #endregion

        /// <summary>
        /// Writes the file specified in the request URL into the response stream.
        /// </summary>
        private void ProcessFileRequest(HttpListenerContext context)
        {
            string filePath = context.Request.Url.AbsolutePath.Substring("/gui/".Length);

            if (filePath.Length == 0)
                filePath = IndexFile;

            filePath = Path.Combine(DirWebUI.FullName, filePath);

            string fileName = Path.GetFileName(filePath);
            switch (fileName)
            {
                case IndexFile: 
                    ProcessIndexFileRequest(context, filePath);
                    break;

                case TokenFile:
                    ProcessTokenRequest(context);
                    break;

                default:
                    ServeFile(context, filePath);
                    break;
            }
        }

        /// <summary>
        /// Serves a file from the WebUI directory or a 404 message.
        /// </summary>
        private void ServeFile(HttpListenerContext context, string path)
        {
            if (!File.Exists(path))
            {
                RespondFileNotFound(context);
                return;
            }

            string ext = Path.GetExtension(path);
            context.Response.ContentType = MimeTypes.ExtensionLookup(ext);
            
            using (FileStream data = File.OpenRead(path))
            {
                if (data.CanSeek)
                    context.Response.ContentLength64 = data.Length;

                byte[] buffer = new byte[1024];
                int count;

                while ((count = data.Read(buffer, 0, buffer.Length)) > 0)
                {
                    context.Response.OutputStream.Write(buffer, 0, count);
                }
            }

            context.Response.Close();
        }

        /// <summary>
        /// Send a "404 Not Found" response.
        /// </summary>
        /// <param name="context"></param>
        private void RespondFileNotFound(HttpListenerContext context)
        {
            Respond(context, HttpStatusCode.NotFound, "404 Not Found.");
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
        /// Processes "http://host:port/gui/?..." requests, this is where WebUI logic is.
        /// </summary>
        private void ProcessQueryRequest(HttpListenerContext context)
        {
            HttpListenerRequest Request = context.Request;
            HttpListenerResponse Response = context.Response;
            
            Response.ContentType = "application/json";
            Response.ContentEncoding = this.ResponseEncoding;
            
            using (TextWriter writer = new StreamWriter(Response.OutputStream, Response.ContentEncoding))
            using (JsonWriter json = new JsonWriter(writer))
            {
#if DEBUG || TRACE
                json.Formatting = Newtonsoft.Json.Formatting.Indented;
#endif
                json.WriteStartObject();

                json.WritePropertyName("build");
                json.WriteValue(BuildNumber);

                string errorMessage = null;
                try
                {
                    HandleActionRequest(context, json);
                }
                catch (ApplicationException ex)
                {
                    Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    errorMessage = "Error: " + ex.Message;
                }
                finally
                {
                    if (!String.IsNullOrEmpty(errorMessage))
                    {
                        json.WritePropertyName("error");
                        json.WriteValue(errorMessage);
                    }
                }

                json.WriteEndObject();
            }
        }

        /// <summary>
        /// Handles /gui/?action=ACTION_NAME&... queries
        /// </summary>
        private void HandleActionRequest(HttpListenerContext context, JsonWriter json)
        {
            HttpListenerRequest Request = context.Request;
            HttpListenerResponse Response = context.Response;

            //string token = Request.QueryString["token"];
            string action = Request.QueryString["action"];
            string hash = Request.QueryString["hash"];
            bool list = String.Equals(Request.QueryString["list"], "1", StringComparison.Ordinal);

            switch (action)
            {
                case "getsettings":
                    Response.ContentType = "text/plain";
                    PrintSettings(json);

                    break;
                case "setsetting":
                    string[] settingNames = Request.QueryString["s"].Split(',');
                    string[] settingValues = Request.QueryString["v"].Split(',');

                    SetSettings(settingNames, settingValues);
                    break;
                case "getprops":
                    PrintProperties(json, hash);
                    break;
                case "setprops":
                    string propName = Request.QueryString["s"];
                    string propValue = Request.QueryString["v"];

                    if (propName == "label")
                        SetLabel(hash, propValue);
                    break;
                case "getfiles":
                    PrintFiles(json, hash);
                    break;
                case "setprio":
                    string fileIndexes = Request.QueryString["f"];
                    int priority = Int32.Parse(Request.QueryString["p"]);
                    SetFilesPriority(hash, fileIndexes, priority);
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
                    RemoveTorrents(json, hash, false);
                    break;
                case "removedata":
                    RemoveTorrents(json, hash, true);
                    break;
                case "recheck":
                    break;
                case "add-file":
                    throw new NotImplementedException("File uploads are not yet supported by this server. Please use URL fetcher.");
                case "add-url":
                    string url = Request.QueryString["s"];                    
                    Uri torrentUri;
                    List<Cookie> cookies;
                    ParseCookieSuffix(url, out torrentUri, out cookies);
                    
                    if(torrentUri != null)
                        AddTorrentFromUrl(torrentUri, cookies);

                    break;

                case null:
                    break; // do nothing

                default:
                    Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    throw new ApplicationException("Unknown request. URL: " + Request.RawUrl);
            }

            if (list)
                PrintTorrentList(json);
        }

        private static void ParseCookieSuffix(string url, out Uri uri, out List<Cookie> cookies)
        {
            // Match url http://some-tracker.com/torrent/12345:COOKIE:abc=123;xyz=789
            Match match = Regex.Match(url, "^(?<url>.*):COOKIE:(?<cookies>(([^=]*=?([^;]*|;|$)))*)");
            Regex cookieParse = new Regex("^(?<key>[^=]+)(=(?<value>[^;]*))?$");

            if (!match.Success)
            {
                uri = null;
                cookies = null;
                return;
            }

            Uri.TryCreate(match.Result("${url}"), UriKind.Absolute, out uri);
            string rawCookies = match.Result("${cookies}");

            cookies = new List<Cookie>();
            foreach(string rawCookie in rawCookies.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
            {
                Match matchCookie = cookieParse.Match(rawCookie);

                string key = matchCookie.Result("${key}");
                string value = matchCookie.Result("${value}");
                cookies.Add(new Cookie(key, value));
            }
        }

        /// <summary>
        /// Prints each registered setting to the <paramref name="writer" />.
        /// Format is consistent with WebUI: ["name",type,"value"]
        /// </summary>
        public void PrintSettings(JsonWriter writer)
        {
            writer.WritePropertyName("settings");
            writer.WriteStartArray();

            foreach (Setting setting in settingsAdapter)
            {
                string value = setting.GetStringValue();

                if (value == null)
                    continue;

                writer.WriteStartArray();

                writer.WriteValue(setting.Name);
                writer.WriteValue((int)setting.Type);
                writer.WriteValue(value);

                writer.WriteEndArray();
            }

            writer.WriteEndArray();
        }

        /// <summary>
        /// Fetches .torrent file from a URL and adds it.
        /// </summary>
        private void AddTorrentFromUrl(Uri uri, IEnumerable<Cookie> cookies)
        {
            HttpWebRequest req = WebRequest.Create(uri) as HttpWebRequest;
            
            if (req == null)
                throw new NotSupportedException("Torrents may only be fetched from HTTP URIs.");

            if (cookies != null)
            {
                req.CookieContainer = new CookieContainer();
                
                foreach (var cookie in cookies)
                    req.CookieContainer.Add(uri, cookie);
            }

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            {
                if (resp.ContentLength > 1048576) // 1 MB
                    throw new ApplicationException("File size is too large.");

                AddFile(resp.GetResponseStream());
            }
        }
        #endregion

        #region WebUI <--> ITorrentController Adapter Methods

        // These functions call the appropriate methods of ITorrentController
        // and print the JSON response data.
        // All calls come from ProcessQueryRequest(...).

        /// <summary>
        /// Sets the setting values.
        /// </summary>
        private void SetSettings(string[] names, string[] values)
        {
            for (int i = 0; i < names.Length && i < values.Length; i++)
                settingsAdapter[names[i]] = values[i];
        }

        /// <summary>
        /// Outputs the corresponding torrent's properties.
        /// </summary>
        private void PrintProperties(JsonWriter writer, string hash)
        {
            TorrentManager details = torrents.GetTorrentManager(hash);

            writer.WritePropertyName("props");
            writer.WriteStartArray();

            if (details != null)
            {
                writer.WriteStartObject();

                int dhtStatus = (int)(details.CanUseDht ? WebUtil.BooleanAdapter(details.Settings.UseDht) : WebOption.NotAllowed);

                writer.WritePropertyName("hash");           //HASH (string)
                writer.WriteValue(hash);
                writer.WritePropertyName("trackers");       //TRACKERS (string)
                writer.WriteValue(WebUtil.GetTrackerString(details));
                writer.WritePropertyName("ulrate");         //UPLOAD LIMIT (integer in bytes per second)
                writer.WriteValue(details.Settings.MaxUploadSpeed);
                writer.WritePropertyName("dlrate");         //DOWNLOAD LIMIT (integer in bytes per second)
                writer.WriteValue(details.Settings.MaxDownloadSpeed);
                writer.WritePropertyName("superseed");      //INITIAL SEEDING (integer)
                writer.WriteValue((int)WebUtil.BooleanAdapter(details.Settings.InitialSeedingEnabled));
                writer.WritePropertyName("dht");            //USE DHT (integer)
                writer.WriteValue(dhtStatus);
                writer.WritePropertyName("pex");            //USE PEX (integer)
                writer.WriteValue((int)WebOption.Disabled);
                writer.WritePropertyName("seed_override");  //OVERRIDE QUEUEING (integer)
                writer.WriteValue((int)WebOption.Disabled);
                writer.WritePropertyName("seed_ratio");     //SEED RATIO (integer in 1/10 of a percent)
                writer.WriteValue(0);
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
            TorrentManager details = torrents.GetTorrentManager(hash);

            writer.WritePropertyName("files");
            writer.WriteStartArray();

            writer.WriteValue(hash);

            if (details != null)
            {
                writer.WriteStartArray();
                foreach (TorrentFile file in details.Torrent.Files)
                {
                    writer.WriteStartArray();

                    writer.WriteValue(file.Path);   //FILE NAME (string)
                    writer.WriteValue(file.Length); //FILE SIZE (integer in bytes)
                    writer.WriteValue(0);           //DOWNLOADED (integer in bytes)
                    writer.WriteValue((int)WebUtil.PriorityAdapter(file.Priority)); //PRIORITY* (integer)

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
            IEnumerable<KeyValuePair<string, int>> labels = torrents.GetAllLabels();

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

            foreach (KeyValuePair<string, TorrentManager> pair in torrents.TorrentManagers)
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
                int status = (int)WebUtil.StateAdapter(torrent.State);

                writer.WriteValue(hash);                                //HASH (string),
                writer.WriteValue(status);                              //STATUS (integer),
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
            torrents.AddTorrent(fileData, null, null);
        }

        /// <summary>
        /// Sets priority of files (specified by indexes) in the torrent (specified by hash).
        /// </summary>
        private void SetFilesPriority(string hash, string fileIndices, int webPriority)
        {
            Priority priority = WebUtil.PriorityAdapter((WebPriority)webPriority);
            string[] splitIndexes = fileIndices.Split(new char[] { ',' });
            List<int> parsedIndexes = new List<int>(splitIndexes.Length);

            foreach (string fileIndex in splitIndexes)
            {
                try { parsedIndexes.Add(int.Parse(fileIndex)); }
                catch { continue; }
            }

            torrents.SetFilePriority(hash,
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
                torrents.StartTorrent(hash);
            }
        }

        /// <summary>
        /// Pauses torrents specified in the comma separated list of hashes.
        /// </summary>
        private void PauseTorrents(string hashes)
        {
            string[] hashList = hashes.Split(new char[] { ',' });

            foreach (string hash in hashList)
            {
                torrents.PauseTorrent(hash);
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
                torrents.StopTorrent(hash);
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
                torrents.RemoveTorrent(hash, removeData);
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
        private void SetLabel(string hash, string label)
        {
            torrents.SetTorrentLabel(hash, label);
        }
        #endregion

        #region Trace Helpers
        [Conditional("TRACE")]
        private static void TraceHttpRequest(HttpListenerContext context)
        {
            TraceWriteLine("HttpListenerRequest");
            TraceWriteLine("{");
            if (context.User != null && context.User.Identity != null)
                TraceWriteLine("   User:     {0} ({1})", context.User.Identity.Name, context.User.Identity.AuthenticationType);
            else
                TraceWriteLine("   User:     null");
            TraceWriteLine("   From:     {1} {0}", context.Request.UserAgent, context.Request.RemoteEndPoint);
            TraceWriteLine("   Request:  {1} {0}", context.Request.RawUrl, context.Request.HttpMethod);
            TraceWriteLine("   Accepts:  {0}", context.Request.AcceptTypes);
            if (context.Request.HasEntityBody)
                TraceWriteLine("   Content:  {0} ({1} bytes)", context.Request.ContentType, context.Request.ContentLength64);
            TraceWriteLine("   Encoding: {0}", context.Request.ContentEncoding);
            TraceWriteLine("   Referrer: {0}", context.Request.UrlReferrer);
            if (context.Request.Headers.Count > 0)
            {
                TraceWriteLine("   Headers:");
                for (int i = 0; i < context.Request.Headers.Count; i++)
                {
                    TraceWriteLine("      {0} = {1}", context.Request.Headers.GetKey(i), context.Request.Headers.Get(i));
                }
            }
            if (context.Request.Cookies.Count > 0)
            {
                TraceWriteLine("   Cookies:");
                foreach (Cookie ck in context.Request.Cookies)
                {
                    TraceWriteLine("      {0}", ck);
                }
            }
            TraceWriteLine("}");
        }

        [Conditional("TRACE")]
        private static void TraceHttpResponse(HttpListenerContext context)
        {
            TraceWriteLine("HttpListenerResponse");
            TraceWriteLine("{");
            TraceWriteLine("   Status:   {0} {1}", context.Response.StatusCode, context.Response.StatusDescription);
            if (!String.IsNullOrEmpty(context.Response.RedirectLocation))
                TraceWriteLine("   Redirect: {0}", context.Response.RedirectLocation);
            TraceWriteLine("   Content:  {0} ({1} bytes)", context.Response.ContentType, context.Response.ContentLength64);
            TraceWriteLine("   Encoding: {0}", context.Response.ContentEncoding);
            //			if(context.Response.Headers.Count > 0)
            //			{
            //				TraceWriteLine("   Headers:");
            //				foreach(Header hd in context.Response.Headers)
            //				{
            //					TraceWriteLine("      {0}", hd);
            //				}
            //			}			
            if (context.Response.Cookies.Count > 0)
            {
                TraceWriteLine("   Cookies:");
                foreach (Cookie ck in context.Response.Cookies)
                {
                    TraceWriteLine("      {0}", ck);
                }
            }
            TraceWriteLine("}");
        }

        [Conditional("TRACE")]
        private static void TraceWriteLine(string message)
        {
            Trace.WriteLine(message);
        }

        [Conditional("TRACE")]
        private static void TraceWriteLine(string format, params object[] args)
        {
            Trace.WriteLine(String.Format(format, args));
        }
        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            StopHttpServer();
        }

        #endregion
    }
}
