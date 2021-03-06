﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using MonoTorrent.Client;
using MonoTorrent.Common;
using MonoTorrent.WebUI.Common;
using MonoTorrent.WebUI.Server.Utility;
using Newtonsoft.Json;
// enumerable of (string:string) pairs
using KeyValueBag = System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>>;
using MonoTorrent.WebUI.Server.Configuration;
using System.Text.RegularExpressions;
using System.Xml;

namespace MonoTorrent.WebUI.Server
{
    /// <summary>
    /// This portion of the class keeps WebUI query handling logic.
    /// </summary>
	class WebUIServer : HttpServer<WebUISection>
	{
        /// <summary>
        /// Maps configuration between WebUI and MonoTorrent.
        /// </summary>
        private SettingsAdapter settingsAdapter;

        /// <summary>
        /// Reference to the MonoTorrent engine.
        /// </summary>
        public ITorrentController<string, TorrentManager> torrents;

        /// <summary>
        /// Build number reported to WebUI.
        /// </summary>
        public int BuildNumber
        {
            get;
            private set;
        }

        /// <summary>
        /// Creates a WebUI server.
        /// </summary>
        public WebUIServer(ITorrentController<string, TorrentManager> torrents)
            : base()
        {
            if (torrents == null)
                throw new ArgumentNullException("torrents");

            this.torrents = torrents;

            BuildNumber = this.GetType().Assembly.GetName().Version.Build;
        }

        protected override void OnStartServer(WebUISection config)
        {
            this.BuildNumber = config.BuildNumber;

            this.settingsAdapter = new SettingsAdapter(config, torrents);
        }

        protected override void OnStopServer()
        {
            this.settingsAdapter = null;
        }

        #region Request Processing
        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        protected override bool HandleRequest(HttpListenerContext context)
        {
            if (RedirectToCanonicalUrl(context))
                return true;

            if (String.IsNullOrEmpty(context.Request.Url.Query))
            {
                return false;
            }
            else
            {
                ProcessQueryRequest(context);
                return true;
            }
        }

        #region Constants
        private const string IndexFile = "index.html";
        private const string TokenFile = "token.html";
        #endregion

        /// <summary>
        /// Reconnizes 
        /// </summary>
        /// <param name="context"></param>
        protected override void ProcessFileRequest(HttpListenerContext context)
        {
            string sitePath = GetWebSitePath(context);
            string filePath;
            
            if (sitePath.Length == 0)
                filePath = Path.Combine(WebSiteRoot.FullName, IndexFile);
            else
                filePath = GetServerFilePath(context);

            string fileName = Path.GetFileName(filePath);
            switch (fileName)
            {
                case IndexFile:
                    ServeIndexFile(context, filePath);
                    break;

                case TokenFile:
                    ServeTokenRequest(context);
                    break;

                default:
                    base.ProcessFileRequest(context);
                    break;
            }
        }

        /// <summary>
        /// Answers with a token number.
        /// </summary>
        private void ServeTokenRequest(HttpListenerContext context)
        {
            const string tokenTemplate =
                "<html>" +
                "<head>" +
                "   <title>Token Response</title>" +
                "</head>" +
                "<body>" +
                "   <div id='token' style='display:none;'>{0}</div>" +
                "</body>" +
                "</html>";

            Guid token = Guid.NewGuid();
            string answer = String.Format(tokenTemplate, Guid.NewGuid());

            context.Response.ContentType = "text/html";
            Respond(context, HttpStatusCode.OK, answer);
        }

        /// <summary>
        /// Serves the WebUI index file.
        /// </summary>
        private void ServeIndexFile(HttpListenerContext context, string filePath)
        {
            XmlDocument indexDocument = XmlFileLoader.LoadXmlDocument(filePath);
            XmlNode tokenNode = indexDocument.SelectSingleNode("//node()[@id='token']");

            if (tokenNode != null)
                tokenNode.InnerText = new Random().Next(100000000, 999999999).ToString();

            string ext = Path.GetExtension(filePath);
            context.Response.ContentType = MimeTypes.ExtensionLookup(ext);
            using (XmlWriter writer = new XmlTextWriter(context.Response.OutputStream, ResponseEncoding))
            {
                indexDocument.WriteTo(writer);
            }
        }

        private static readonly Regex nonCanonicalUrl = new Regex("^/gui([?])?$", RegexOptions.Compiled);

        /// <summary>
        /// Redirects ListeningAddress requests with missing slashes
        /// </summary>
        private bool RedirectToCanonicalUrl(HttpListenerContext context)
        {
            // redirects /gui  or /gui?<query>
            // to        /gui/ or /gui/?<query>

            if (nonCanonicalUrl.IsMatch(context.Request.RawUrl))
            {
                string fixedUrl = context.Request.RawUrl.Insert("/gui".Length, "/");

                context.Response.StatusCode = (int)HttpStatusCode.MovedPermanently;
                context.Response.Redirect(fixedUrl);

                return true;
            }
            else
            {
                return false;
            }
        } 

        /// <summary>
        /// Processes "http://host:port/gui/?..." requests, this is where WebUI logic is.
        /// </summary>
        private void ProcessQueryRequest(HttpListenerContext context)
        {
            if (!ValidateQueryToken(context))
                return;

            context.Response.ContentType = MimeTypes.Json;
            context.Response.ContentEncoding = ResponseEncoding;

            using (TextWriter writer = new StreamWriter(context.Response.OutputStream, ResponseEncoding))
            using (JsonTextWriter json = new JsonTextWriter(writer))
            {
                json.WriteStartObject();

                json.WritePropertyName("build");
                json.WriteValue(BuildNumber);

                try
                {
                    AnswerQuery(context, json);
                }
                catch (ApplicationException ex)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    PrintError(json, ex);
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    PrintError(json, ex);
                }

                json.WriteEndObject();
            }
        }

        private static void PrintError(JsonWriter json, Exception ex)
        {
            json.WritePropertyName("error");
            json.WriteValue("Error: " + ex.Message);
        }

        /// <summary>
        /// Validates the token passed with the request.
        /// </summary>
        /// <returns>True if the token is valid, otherwise the request was answered with HTTP 400.</returns>
        private bool ValidateQueryToken(HttpListenerContext context)
        {
            //TODO: Implement tokens

            string token = context.Request.QueryString["token"];

            //if (String.IsNullOrEmpty(token))
            //{
            //    ProcessBadRequest(context, "Invalid token used in request.");
            //    return false;
            //}
            //else
            //    return true;

            return true;
        }

        /// <summary>
        /// Handles /gui/?action=ACTION_NAME&... queries
        /// </summary>
        private void AnswerQuery(HttpListenerContext context, JsonWriter json)
        {
            HttpListenerRequest Request = context.Request;
            HttpListenerResponse Response = context.Response;

            string action = Request.QueryString["action"];
            string[] hashes = WebUtil.ParseList(Request, "hash");
            string hash = hashes.Length > 0 ? hashes[0] : null;
            string[] fileIndeces = WebUtil.ParseList(Request, "f");
            bool list = WebUtil.TestBooleanField(Request, "list");

            switch (action)
            {
                case "getsettings":
                    PrintSettings(json);
                    break;
                case "setsetting":
                    KeyValueBag parmeters = WebUtil.ParseParallelLists(
                        Request, "s", "v"
                        );
                    SetSettings(parmeters);
                    break;

                case "getprops":
                    PrintProperties(json, hashes);
                    break;
                case "setprops":
                    var parameters = WebUtil.ParsePropertyChanges(Request.Url.Query);
                    SetTorrentProperties(parameters);
                    break;

                case "getfiles":
                    PrintTorrentFiles(json, hash);
                    break;
                case "setprio":
                    WebPriority priority = WebUtil.ParseWebPriority(
                        Request.QueryString["p"]
                        );
                    SetFilePriorities(hash, fileIndeces, priority);
                    break;

                case "start":
                    StartTorrents(hashes);
                    break;
                case "forcestart":
                    StartTorrents(hashes);
                    break;
                case "stop":
                    StopTorrents(hashes);
                    break;
                case "pause":
                    PauseTorrents(hashes);
                    break;
                case "unpause":
                    StartTorrents(hashes);
                    break;
                case "remove":
                    RemoveTorrents(json, hashes, false);
                    break;
                case "removedata":
                    RemoveTorrents(json, hashes, true);
                    break;
                case "recheck":
                    RecheckTorrents(hashes);
                    break;

                case "add-file":
                    AddTorrentFromUpload(Request);
                    break;
                case "add-url":
                    string url = Request.QueryString["s"];
                    AddTorrentFromUrl(url);
                    break;

                case null: // no "action" parameter
                    break; // do nothing

                default:
                    throw new ApplicationException("Unknown query.");
            }

            if (list)
                PrintTorrentList(json);
        }

        /// <summary>
        /// Prints each registered setting to the <paramref name="writer" />.
        /// Format is consistent with WebUI: ["name",type,"value"]
        /// </summary>
        private void PrintSettings(JsonWriter writer)
        {
            writer.WritePropertyName("settings");
            writer.WriteStartArray();

            foreach (Setting setting in settingsAdapter)
            {
                string value = setting.GetStringValue();

                if (value == null)
                    continue;

                try
                {
                    writer.WriteStartArray();

                    writer.WriteValue(setting.Name);
                    writer.WriteValue((int)setting.Type);
                    writer.WriteValue(value);

                    writer.WriteEndArray();
                }
                catch (Exception ex)
                {
                    TraceWriteLine(ex.ToString());
                }
            }

            writer.WriteEndArray();
        }
        #endregion

        #region WebUI <--> ITorrentController Adapter Methods
        /// <summary>
        /// Sets the setting values.
        /// </summary>
        private void SetSettings(KeyValueBag settings)
        {
            foreach (var setting in settings)
                settingsAdapter[setting.Key] = setting.Value;
        }

        /// <summary>
        /// Prints the properties of each specified torrent.
        /// </summary>
        private void PrintProperties(JsonWriter writer, string[] hashes)
        {
            writer.WritePropertyName("props");
            writer.WriteStartArray();

            foreach (string hash in hashes)
            {
                PrintTorrentProperties(writer, hash);
            }

            writer.WriteEndArray();
        }

        /// <summary>
        /// Prints properties of one torrent.
        /// </summary>
        private void PrintTorrentProperties(JsonWriter writer, string hash)
        {
            writer.WriteStartObject();

            writer.WritePropertyName("hash");               //HASH (string)
            writer.WriteValue(hash);

            TorrentManager details = torrents.GetTorrent(hash);

            if (details != null)
            {
                int dhtStatus = (int)(details.CanUseDht ? WebUtil.BooleanAdapter(details.Settings.UseDht) : WebOption.NotAllowed);

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
            }
            else
                writer.WriteNull();

            writer.WriteEndObject();
        }

        /// <summary>
        /// Outputs the corresponding torrent's files.
        /// </summary>
        private void PrintTorrentFiles(JsonWriter writer, string hash)
        {
            TorrentManager details = torrents.GetTorrent(hash);

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

            foreach (KeyValuePair<string, TorrentManager> pair in torrents.Torrents)
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
        /// Sets priority of files (specified by indexes) in the torrent (specified by hash).
        /// </summary>
        private void SetFilePriorities(string hash, string[] fileIndices, WebPriority priority)
        {
            List<int> parsedIndeces = new List<int>(fileIndices.Length);

            foreach (string fileIndex in fileIndices)
            {
                int index;

                if (int.TryParse(fileIndex, out index))
                    parsedIndeces.Add(index);
            }

            torrents.SetFilePriorities(hash,
                parsedIndeces.ToArray(),
                (int)WebUtil.PriorityAdapter(priority));
        }

        /// <summary>
        /// Starts torrents specified in the comma separated list of hashes.
        /// </summary>
        private void StartTorrents(string[] hashes)
        {
            foreach (string hash in hashes)
            {
                torrents.StartTorrent(hash);
            }
        }

        /// <summary>
        /// Pauses torrents specified in the comma separated list of hashes.
        /// </summary>
        private void PauseTorrents(string[] hashes)
        {
            foreach (string hash in hashes)
            {
                torrents.PauseTorrent(hash);
            }
        }

        /// <summary>
        /// Stops torrents specified in the comma separated list of hashes.
        /// </summary>
        private void StopTorrents(string[] hashes)
        {
            foreach (string hash in hashes)
            {
                torrents.StopTorrent(hash);
            }
        }

        /// <summary>
        /// Removes torrents specified in the comma separated list of hashes.
        /// </summary>
        private void RemoveTorrents(JsonWriter writer, string[] hashes, bool removeData)
        {
            writer.WritePropertyName("torrentm");
            writer.WriteStartArray();

            foreach (string hash in hashes)
            {
                if (torrents.RemoveTorrent(hash, removeData))
                    writer.WriteValue(hash);
            }

            writer.WriteEndArray();
        }

        /// <summary>
        /// Initiates a recheck of the downloaded data.
        /// </summary>
        private void RecheckTorrents(string[] hashes)
        {
            foreach(string hash in hashes)
                torrents.RecheckTorrentData(hash);
        }

        /// <summary>
        /// Reads the torrent metadata in the request entity.
        /// </summary>
        private void AddTorrentFromUpload(HttpListenerRequest request)
        {
            if (request.HttpMethod != "POST")
                throw new ApplicationException("Uploads must use HTTP POST requests.");

            if (!request.HasEntityBody)
                throw new ApplicationException("File was not included int the request.");

            throw new NotImplementedException(
                "File uploads are not yet supported by this server. Please use URL fetcher."
                );
        }

        /// <summary>
        /// Sets properties of each torrent.
        /// </summary>
        /// <param name="changes"></param>
        private void SetTorrentProperties(IEnumerable<KeyValuePair<string, KeyValueBag>> changes)
        {
            foreach (var change in changes)
            {
                foreach (var prop in change.Value)
                {
                    SetTorrentProperty(change.Key, prop.Key, prop.Value);
                }
            }
        }

        /// <summary>
        /// Sets the specified property of the specified torrent.
        /// </summary>
        private void SetTorrentProperty(string hash, string propName, string propValue)
        {
            switch (propName)
            {
                case "label":
                    torrents.SetTorrentLabel(hash, propValue);
                    break;
            }
        }

        /// <summary>
        /// Fetches .torrent file from a ListeningAddress and adds it.
        /// </summary>
        private void AddTorrentFromUrl(string url)
        {
            byte[] data = WebUtil.FetchUrlWithCookies(url, 1048576);

            torrents.AddTorrent(data, null, null);
        }
        #endregion
	}
}
