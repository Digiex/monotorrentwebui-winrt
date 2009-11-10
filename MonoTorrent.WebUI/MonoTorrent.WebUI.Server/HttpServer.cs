using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using MonoTorrent.WebUI.Common;
using MonoTorrent.WebUI.Server.Configuration;
using MonoTorrent.WebUI.Server.Utility;

namespace MonoTorrent.WebUI.Server
{
	/// <summary>
	/// HTTP server that handles WebUI queries.
	/// </summary>
    public partial class HttpServerWebUI : IDisposable
	{
        /// <summary>
        /// This is the webserver.
        /// </summary>
        private HttpListener httpListener;

        /// <summary>
        /// Thread which runs ListenLoop()
        /// </summary>
        private Thread listenWorker;

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
        /// Determines if the request was made from the local machine.
        /// This method is used instead of HttpListenerRequest.IsLocal because of a bug.
        /// </summary>
        /// <returns>True is the request local and remote end points are the same, false otherwise.</returns>
        private static bool IsLocal(HttpListenerRequest request)
        {
            return request.LocalEndPoint.Address.Equals(request.RemoteEndPoint.Address);
        }
        #endregion

        #region HTTP Request Handling
        #region Listen Loop
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
        #endregion

        #region Request Marshalling
        private static readonly Regex queryUrl = new Regex("^/gui/[?]", RegexOptions.Compiled);
        private static readonly Regex fileUrl = new Regex("^/gui/", RegexOptions.Compiled);

        /// <summary>
        /// Dispatches the request to the appropriate handler based on the URL
        /// </summary>
        private void MarshalRequest(HttpListenerContext context)
        {
            if (RedirectToCanonicalUrl(context))
                return; // request redirected from /gui... to /gui/...

            if (queryUrl.IsMatch(context.Request.RawUrl))
                ProcessQueryRequest(context); // /gui/some/path?token=...&action=...&hash=...
            else if (fileUrl.IsMatch(context.Request.RawUrl))
                ProcessFileRequest(context); // /gui/some/file.ext
            else
                ProcessBadRequest(context, "We don't serve that here!");
        }

        /// <summary>
        /// Writes a message as the response.
        /// </summary>
        private void Respond(HttpListenerContext context, HttpStatusCode httpStatusCode, string message)
        {
            context.Response.StatusCode = (int)httpStatusCode;
            context.Response.ContentEncoding = ResponseEncoding;

            byte[] data = ResponseEncoding.GetBytes(message);

            context.Response.Close(data, false);
        }

        /// <summary>
        /// Responds with HTTP 400 and the specified message.
        /// </summary>
        private void ProcessBadRequest(HttpListenerContext context, string message)
        {
            Respond(context, HttpStatusCode.BadRequest, message);
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
        #endregion

        #region Static File Requests
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
                    ServeIndexFile(context, filePath);
                    break;

                case TokenFile:
                    ServeTokenRequest(context);
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
                tokenNode.InnerText = new Random().Next(100000000,999999999).ToString();

            context.Response.ContentType = System.Net.Mime.MediaTypeNames.Text.Html;
            using (XmlWriter writer = new XmlTextWriter(context.Response.OutputStream, ResponseEncoding))
            {
                indexDocument.WriteTo(writer);
            }
        } 
        #endregion
        #endregion
        
        #region Trace Helpers
        [Conditional("TRACE")]
        private static void TraceHttpRequest(HttpListenerContext context)
        {
            TraceWriteLine("HttpListenerRequest");
            TraceWriteLine("{");
            TraceWriteLine("   {1} {0}", context.Request.RawUrl, context.Request.HttpMethod);
            if (context.User != null && context.User.Identity != null)
                TraceWriteLine("   User:     {0} ({1})", context.User.Identity.Name, context.User.Identity.AuthenticationType);
            else
                TraceWriteLine("   User:     null");
            TraceWriteLine("   From:     {1} {0}", context.Request.UserAgent, context.Request.RemoteEndPoint);
            if (context.Request.HasEntityBody)
                TraceWriteLine("   Content:  {0} ({1} bytes)", context.Request.ContentType, context.Request.ContentLength64);
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
