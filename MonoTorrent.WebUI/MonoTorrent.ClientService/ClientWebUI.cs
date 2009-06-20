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
using MonoTorrent.Client.Tracker;
using MonoTorrent.ClientService.Configuration;

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
        /// Configuration for WebUI.
        /// </summary>
        WebUISection config;

        /// <summary>
        /// This is the webserver.
        /// </summary>
        private HttpListener listener;
        
        /// <summary>
        /// Service which is running the MonoTorrent engine
        /// </summary>
        private MonoTorrentClient service;

        // Cache the index document as an XmlDocument because 
        // XmlDocument.Load() takes too long to process for each request
        private XmlDocument indexDocument;
        private XmlNode indexTokenNode;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="filesWebUI">Directory containing the WebUI files. There must be an "index.html" present.</param>
        /// <param name="listenPrefix">Prefix to register with HttpListener. It must be of the form "http://hostname:port/gui/", hostname may be a wildcard (* or +), port is optional.</param>
        /// <param name="monoTorrentService">An instance of the MonoTorrent client service.</param>
        public ClientWebUI(MonoTorrentClient monoTorrentService)
        {
            if (monoTorrentService == null)
                throw new ArgumentNullException("monoTorrentService");

            this.CanPauseAndContinue = true;
            this.ServiceName = "MonoTorrent Web UI";

            listener = new HttpListener();
            service = monoTorrentService;
            listenWorker = new Thread(ListenLoop);
        }

        /// <summary>
        /// Loads the WebUI configuration section.
        /// </summary>
        private void LoadConfiguration(string[] args)
        {
            if (args.Length > 0)
            {
                System.Configuration.Configuration configFile =
                    ConfigurationManager.OpenExeConfiguration(args[0]);

                config = (WebUISection)configFile.GetSection("WebUI");
            }
            else
                config = (WebUISection)ConfigurationManager.GetSection("WebUI");
        }

        /// <summary>
        /// Applies the currently loaded configuration section.
        /// </summary>
        private void ApplyConfiguration()
        {
            listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;

            listener.Prefixes.Clear();
            listener.Prefixes.Add(config.HttpListenerPrefix);
        }

        #region Service Control

        private object serviceControlLock = new object();

        protected override void OnStart(string[] args)
        {
            lock (serviceControlLock)
            {
                LoadConfiguration(args);
                ApplyConfiguration();

                stopFlag = false;
                LoadIndexDocument();

                listener.Start();
                listenWorker.Start();
            }
        }

        protected override void OnStop()
        {
            lock (serviceControlLock)
            {
                stopFlag = true;
                listener.Stop();

                // wait for request to finish processing
                if (!listenWorker.Join(5000))
                {
                    // taking too long
                    listener.Abort(); // drop all incoming request and shutdown
                    listenWorker.Join();
                }
            }
        }

        protected override void OnPause()
        {
            lock (serviceControlLock)
            {
                Monitor.Enter(requestProcessLock);
            }
        }

        protected override void OnContinue()
        {
            lock (serviceControlLock)
            {
                Monitor.Exit(requestProcessLock);
            }
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
					TraceWriteLine("Waiting for HTTP request...");
                    context = listener.GetContext();

					TraceHttpRequest(context);
					
                    lock (requestProcessLock)
                    {
                        MarshalRequest(context);
                    }
                    
                    TraceHttpResponse(context);
                }
                catch (ObjectDisposedException) { } // listener.Abort() was called
                catch (Exception ex)
                {
                    Trace.WriteLine("Error: " + ex.ToString());
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
		
		[Conditional("TRACE")]
		private static void TraceHttpRequest(HttpListenerContext context)
		{
			TraceWriteLine("HttpListenerRequest");
			TraceWriteLine("{");
			if(context.User != null && context.User.Identity != null)
				TraceWriteLine("   User:     {0} ({1})", context.User.Identity.Name, context.User.Identity.AuthenticationType);
			else 
				TraceWriteLine("   User:     null");
			TraceWriteLine("   From:     {1} {0}", context.Request.UserAgent, context.Request.RemoteEndPoint);
			TraceWriteLine("   Request:  {1} {0}", context.Request.RawUrl, context.Request.HttpMethod);
			TraceWriteLine("   Accepts:  {0}", context.Request.AcceptTypes);
			if(context.Request.HasEntityBody)
				TraceWriteLine("   Content:  {0} ({1} bytes)", context.Request.ContentType, context.Request.ContentLength64);
			TraceWriteLine("   Encoding: {0}", context.Request.ContentEncoding);
			TraceWriteLine("   Referrer: {0}", context.Request.UrlReferrer);
			if(context.Request.Headers.Count > 0)
			{
				TraceWriteLine("   Headers:");
				for(int i = 0; i < context.Request.Headers.Count; i++)
				{
					TraceWriteLine("      {0} = {1}", context.Request.Headers.GetKey(i), context.Request.Headers.Get(i));
				}
			}
			if(context.Request.Cookies.Count > 0)
			{
				TraceWriteLine("   Cookies:");
				foreach(Cookie ck in context.Request.Cookies)
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
			if(!String.IsNullOrEmpty(context.Response.RedirectLocation))
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
			if(context.Response.Cookies.Count > 0)
			{
				TraceWriteLine("   Cookies:");
				foreach(Cookie ck in context.Response.Cookies)
				{
					TraceWriteLine("      {0}", ck);
				}
			}
			TraceWriteLine("}");
		}
		
		private static void TraceWriteLine(string message)
		{
			Trace.WriteLine(message);
		}
		
		private static void TraceWriteLine(string format, params object[] args)
		{
			Trace.WriteLine(String.Format(format, args));
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
            //string query = null;

            int queryStart = filePath.IndexOf('?');
            if (queryStart > -1)
            {
                //query = filePath.Substring(queryStart);
                filePath = filePath.Substring(0, filePath.Length - queryStart);
            }

            if (String.IsNullOrEmpty(filePath))
                filePath = indexFile;

            filePath = Path.Combine(config.DirWebUI.FullName, filePath);
            string fileName = Path.GetFileName(filePath);

            if (String.CompareOrdinal(fileName, indexFile) == 0)
                ProcessIndexFileRequest(context, filePath);
            else if (String.CompareOrdinal(fileName, tokenFile) == 0)
                ProcessTokenRequest(context);
            else if (File.Exists(filePath))
            {
            	//TODO: Set Response.ContentType
            	
                using (FileStream data = File.OpenRead(filePath))
                {
                	if(data.CanSeek)
                		context.Response.ContentLength64 = data.Length;
                    
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
            Response.ContentEncoding = config.ResponseEncoding;
            
            using (JsonWriter jsonWriter = new JsonWriter(new StreamWriter(Response.OutputStream, Response.ContentEncoding)))
            {
                //string token = Request.QueryString["token"];
                string action = Request.QueryString["action"];
                string hash = Request.QueryString["hash"];
                bool list = String.CompareOrdinal(Request.QueryString["list"], "1") == 0;

                List<string> errorMessages = new List<string>();
                
                jsonWriter.WriteStartObject();

                jsonWriter.WritePropertyName("build");
                jsonWriter.WriteValue(config.BuildNumber);

                try
                {
                    switch (action)
                    {
                        case "getsettings":
                            string settingsFile = Path.Combine(config.DirWebUI.FullName, "StaticSettings.txt");
                                                        
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
                            jsonWriter.WriteValue("file upload message");
                            
                            if(Request.HasEntityBody)
                            {
	                            TraceWriteLine("*-*-*-*-*-*-* Uploaded File *-*-*-*-*-*-*");
	                            string tempFile = Path.GetTempFileName();
	                            TraceWriteLine(tempFile);
	                             
	                            using(FileStream uploadWriter = File.OpenWrite(tempFile))
	                            {
	                            	
	                            	                    
	                            	byte[] dataBuf = new byte[1024];
	                            	int count;
	                            	int total = 0;
	                            	Request.ContentEncoding.GetString(dataBuf, 0, dataBuf.Length);
	                            	while((count = Request.InputStream.Read(dataBuf, 0, dataBuf.Length)) > 0)
	                            	{
	                            		uploadWriter.Write(dataBuf, 0, count);
	                            		total += count;
	                            	}
	                            	TraceWriteLine("Copied {0} bytes.", total);
	                            }
	                            TraceWriteLine("*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*-*");
                            }
                            else
                            {
                            	TraceWriteLine("add-file: No entity body in request!");
							}
                            break;
                        case "add-url":
                            string url = Request.QueryString["s"];
                            using(System.Net.WebClient web = new System.Net.WebClient())
                            {
                            	byte[] data = web.DownloadData(url);
                            	
                            	AddFile(new MemoryStream(data, 0, data.Length, false, false));
                            }
                            break;

                        case null: 
                            break; // do nothing

                        default:
                            Response.StatusCode = (int)HttpStatusCode.BadRequest;
                            errorMessages.Add("Unknown request. URL: " + Request.RawUrl);
                            break;
                    }

                    if (list)
                        PrintTorrentList(jsonWriter);
                }
                catch (Exception e)
                {
                    base.EventLog.WriteEntry("Parser error", 
                        EventLogEntryType.Error, 
                        0, 0, 
                        Encoding.Default.GetBytes(e.ToString())
                        );

                    Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    errorMessages.Add("Server Error: " + e.Message);
                }

                if (errorMessages.Count > 0)
                {
                    string errorMessage = String.Join("\n", errorMessages.ToArray());
                    jsonWriter.WritePropertyName("error");
                    jsonWriter.WriteValue(errorMessage);
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

                int dhtStatus = (int)(details.CanUseDht ? BooleanAdapter(details.Settings.UseDht) : WebOption.NotAllowed);

                writer.WritePropertyName("hash");           //HASH (string)
                writer.WriteValue(hash);
                writer.WritePropertyName("trackers");       //TRACKERS (string)
                writer.WriteValue(GetTrackerString(details));
                writer.WritePropertyName("ulrate");         //UPLOAD LIMIT (integer in bytes per second)
                writer.WriteValue(details.Settings.MaxUploadSpeed);
                writer.WritePropertyName("dlrate");         //DOWNLOAD LIMIT (integer in bytes per second)
                writer.WriteValue(details.Settings.MaxDownloadSpeed);
                writer.WritePropertyName("superseed");      //INITIAL SEEDING (integer)
                writer.WriteValue((int)BooleanAdapter(details.Settings.InitialSeedingEnabled));
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

        private string GetTrackerString(TorrentManager torrent)
        {
            const string newLine = "\r\n";
            StringBuilder value = new StringBuilder();
            foreach (TrackerTier tier in torrent.TrackerManager.TrackerTiers)
            {
                foreach (MonoTorrent.Client.Tracker.Tracker tracker in tier)
                {
                    value.Append(tracker.Uri);
                    value.Append(newLine);
                }

                value.Append(newLine); // tier separator
            }

            return value.ToString();
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
        private WebState StateAdapter(TorrentState state)
        {
            if (state == TorrentState.Paused)
            {
                return WebState.Paused;
            }
            else if ((state == TorrentState.Hashing))
            {
                return WebState.Queued;
            }
            else if ((state == TorrentState.Downloading) || (state == TorrentState.Seeding))
            {
                return WebState.Active;
            }
            else
            {
                return WebState.Stopped;
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
        /// Converts a boolean value 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private WebOption BooleanAdapter(bool? value)
        {
            if (value.HasValue)
                return value.Value ? WebOption.Enabled : WebOption.Disabled;
            else
                return WebOption.NotAllowed;
        }

        /// <summary>
        /// Helper method to load the index.html into an XmlDocument. Done OnStart() because it's rather slow.
        /// </summary>
        private void LoadIndexDocument()
        {
            indexDocument = new XmlDocument();

            lock (indexDocument)
            {
                string indexDocumentPath = Path.Combine(config.DirWebUI.FullName, indexFile);
                using (FileStream data = File.OpenRead(indexDocumentPath))
                {
                    indexDocument.Load(data);
                    indexTokenNode = indexDocument.SelectSingleNode("//*[@id='token']");
                }
            }
        }

        enum WebState : int
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

        enum WebOption : int
        {
            NotAllowed = -1,
            Disabled = 0,
            Enabled = 1
        }
        #endregion
    }
}
