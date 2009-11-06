using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Net;
using System.IO;
using Newtonsoft.Json;
using System.Threading;
using System.Xml;
using System.Text.RegularExpressions;

namespace MonoTorrent.ClientService
{
	/// <summary>
	/// WebUI HTTP server implementation
	/// </summary>
    partial class ClientWebUI
	{
        /// <summary>
        /// This is the webserver.
        /// </summary>
        private HttpListener listener;

        /// <summary>
        /// Thread which runs ListenLoop()
        /// </summary>
        Thread listenWorker;

        /// <summary>
        /// Initializes HTTP server memebers.
        /// </summary>
        private void InitializeHttpServer()
        {
            this.listener = new HttpListener();
            this.listener.AuthenticationSchemeSelectorDelegate = ChooseAuthScheme;

            this.listenWorker = new Thread(ListenLoop);
        }

        #region Configuration
        protected override void ApplyConfiguration()
        {
            listener.Prefixes.Clear();
            listener.Prefixes.Add(Config.HttpListenerPrefix);
        }

        protected override void CollectConfiguration()
        {
            IEnumerator<string> prefixes = listener.Prefixes.GetEnumerator();
            if (prefixes.MoveNext())
                Config.HttpListenerPrefix = prefixes.Current;
            else
                Config.HttpListenerPrefix = null;
        }
        #endregion

        #region State Control API
        private void StartHttpServer()
        {
            stopFlag = false;
            LoadIndexDocument();

            listener.Start();
            listenWorker.Start();
        }

        private void StopHttpServer()
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

        private void PauseHttpServer()
        {
            Monitor.Enter(requestProcessLock);
        }

        private void ResumeHttpServer()
        {
            Monitor.Exit(requestProcessLock);
        } 
        #endregion

        #region HTTP Authentication
        /// <summary>
        /// Determines the authentication method to use for this request.
        /// </summary>
        static AuthenticationSchemes ChooseAuthScheme(HttpListenerRequest request)
        {
            // Do not authenticate local machine requests.
            if (request.RemoteEndPoint.Address.Equals(IPAddress.Loopback) ||
                request.RemoteEndPoint.Address.Equals(IPAddress.IPv6Loopback))
                return AuthenticationSchemes.Anonymous;
            else
                return AuthenticationSchemes.Negotiate;
        }
        #endregion

        #region HTTP Request Handling

        #region Constants
        private const string indexFile = "index.html";
        private const string tokenFile = "token.html";
        #endregion

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
                string indexDocumentPath = Path.Combine(Config.DirWebUI.FullName, indexFile);
                using (FileStream data = File.OpenRead(indexDocumentPath))
                {
                    TraceWriteLine("Loading {0}...", indexFile);
                    indexDocument.Load(data);
                    indexTokenNode = indexDocument.SelectSingleNode("//*[@id='token']");
                    TraceWriteLine("File {0} cached in memory.", indexFile);
                }
            }
        }
        #endregion

        /// <summary>
        /// Set to true to signal the listner loop to halt.
        /// </summary>
        private bool stopFlag = false;

        /// <summary>
        /// Used to pause and unpause the listener thread
        /// </summary>
        private object requestProcessLock = new object();

        /// <summary>
        /// Check stopFlag, listen for request, marshal request, repeat.
        /// </summary>
        private void ListenLoop()
        {
            HttpListenerContext context = null;

            while (!stopFlag)
            {
                // MonoTorrent APIs are not thread-safe and a mutex here
                // will prevent requests from doing stuff concurrently.
                // Parallel HTTP request processing is not really needed
                // since this is a single user system for the most part.

                try
                {
                    Monitor.Enter(requestProcessLock);

                    TraceWriteLine("Waiting for HTTP request...");
                    context = listener.GetContext();

                    TraceHttpRequest(context);

                    MarshalRequest(context);

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

                    Monitor.Exit(requestProcessLock);
                }
            }
        }

        /// <summary>
        /// Dispatches the request to the appropriate handler based on the URL
        /// </summary>
        private void MarshalRequest(HttpListenerContext context)
        {
            if (Regex.IsMatch("^/gui/", context.Request.RawUrl))
            {
                if (context.Request.QueryString.Count > 0)
                    ProcessQueryRequest(context); // /gui/some/path?token=...&action=...&hash=...
                else
                    ProcessFileRequest(context); // /gui/some/file.ext
            }
            else if (Regex.IsMatch("^/gui([?].*)?$", context.Request.RawUrl))
            {
                // Request is for /gui or /gui?<query>
                // should be /gui/ or /gui/?<query>, 

                string fixedUrl = context.Request.RawUrl.Insert("/gui".Length, "/");

                context.Response.StatusCode = (int)HttpStatusCode.MovedPermanently;
                context.Response.Redirect(fixedUrl);
            }
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
        private void ProcessFileRequest(HttpListenerContext context)
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

            filePath = Path.Combine(Config.DirWebUI.FullName, filePath);
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
                    if (data.CanSeek)
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
        /// Processes "http://host:port/gui/?..." requests, this is where WebUI logic is.
        /// </summary>
        private void ProcessQueryRequest(HttpListenerContext context)
        {
            HttpListenerRequest Request = context.Request;
            HttpListenerResponse Response = context.Response;

            if (!Request.RawUrl.StartsWith(urlBaseWithQuery)) // request is for a file + query string
            {
                ProcessFileRequest(context);
                return;
            }

            Response.ContentType = "application/json";
            Response.ContentEncoding = Config.ResponseEncoding;

            using (JsonWriter jsonWriter = new JsonWriter(new StreamWriter(Response.OutputStream, Response.ContentEncoding)))
            {
#if DEBUG
                jsonWriter.Formatting = Newtonsoft.Json.Formatting.Indented;
#endif

                //string token = Request.QueryString["token"];
                string action = Request.QueryString["action"];
                string hash = Request.QueryString["hash"];
                bool list = String.CompareOrdinal(Request.QueryString["list"], "1") == 0;

                List<string> errorMessages = new List<string>();

                jsonWriter.WriteStartObject();

                jsonWriter.WritePropertyName("build");
                jsonWriter.WriteValue(Config.BuildNumber);

                try
                {
                    switch (action)
                    {
                        case "getsettings":
                            Response.ContentType = "text/plain";
                            
                            PrintSettings(jsonWriter);
                            break;
                        case "setsetting":
                            string[] settingNames = Request.QueryString["s"].Split(',');
                            string[] settingValues = Request.QueryString["v"].Split(',');

                            SetSettings(settingNames, settingValues);
                            break;
                        case "getprops":
                            PrintProperties(jsonWriter, hash);
                            break;
                        case "setprops":
                            string propName = Request.QueryString["s"];
                            string propValue = Request.QueryString["v"];

                            if (propName == "label")
                                SetLabel(hash, propValue);
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
                            //errorMessages.Add("File uploads are not yet supported by the server. Please use URL fetcher.");
                            
                            int count = 0;
                            byte[] buffer = new byte[1024];
                            using(FileStream dump = File.OpenWrite("fileupload.bin"))
                                while((count = Request.InputStream.Read(buffer, 0, buffer.Length)) != 0)
                                    dump.Write(buffer, 0, count);

                            break;
                        case "add-url":
                            string url = Request.QueryString["s"];
                            using (System.Net.WebClient web = new System.Net.WebClient())
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
                    this.EventLog.WriteEntry("Parser error",
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
        #endregion
	}
}
