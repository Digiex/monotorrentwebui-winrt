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
    public class HttpServer<TConfig> : IDisposable
        where TConfig : HttpServerSection
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
            private set;
        }

        /// <summary>
        /// WebUI root directory.
        /// </summary>
        public DirectoryInfo WebSiteRoot
        {
            get;
            private set;
        }

        /// <summary>
        /// True if the server is listening, false otherwise.
        /// </summary>
        public bool IsRunning
        {
            get { return httpListener.IsListening; }
        }

        /// <summary>
        /// Base ListeningAddress of the website, derived from the HTTP listener prefix. (e.g. /gui/)
        /// </summary>
        public string WebSiteUrlBase
        {
            get;
            private set;
        }

        /// <summary>
        /// Initializes a WebUI HTTP server.
        /// </summary>
        public HttpServer()
        {
            this.httpListener = new HttpListener();
            this.httpListener.AuthenticationSchemeSelectorDelegate = SelectAuthScheme;

            this.listenWorker = new Thread(ListenLoop);
            this.listenWorker.Name = "HTTP Listener Thread";

            ResponseEncoding = Encoding.Default;
        }

        #region State Control API
        /// <summary>
        /// Begin listening for HTTP requests.
        /// </summary>
        /// <param name="listenerPrefix">HTTP Listener prefix, see <see cref="System.Net.HttpListener"/>.</param>
        /// <param name="dirWebUI">Directory containing WebUI files.</param>
        /// <param name="settingsAdapter">SettingsAdapter instance.</param>
        public void StartHttpServer(TConfig config)
        {
            if (httpListener.IsListening)
                throw new InvalidOperationException("Server is currently running.");
            
            if (config == null)
                throw new ArgumentNullException("config");
            
            this.WebSiteRoot = config.WebSiteRoot;
            this.ResponseEncoding = config.ResponseEncoding;
            this.WebSiteUrlBase = config.HttpListenerPath;

            httpListener.Prefixes.Clear();
            httpListener.Prefixes.Add(config.HttpListenerPrefix);

            stopFlag = false;

            OnStartServer(config);

            httpListener.Start();
            listenWorker.Start();
        }

        protected virtual void OnStartServer(TConfig config)
        {
        }

        /// <summary>
        /// Stop listening for HTTP requests.
        /// </summary>
        public void StopHttpServer()
        {
            if (!httpListener.IsListening)
                return;

            OnStopServer();

            stopFlag = true;
            httpListener.Stop();

            // wait for request to finish processing
            if (!listenWorker.Join(5000))
            {
                // taking too long
                httpListener.Abort(); // drop all incoming request and shutdown
                listenWorker.Join();
            }
        }

        protected virtual void OnStopServer()
        {
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
        /// <summary>
        /// Dispatches the request to the appropriate handler based on the ListeningAddress
        /// </summary>
        private void MarshalRequest(HttpListenerContext context)
        {
            if (HandleRequest(context))
                return;
            else
                ProcessFileRequest(context);
        }

        protected virtual bool HandleRequest(HttpListenerContext context)
        {
            return false;
        }

        /// <summary>
        /// Writes a message as the response.
        /// </summary>
        protected void Respond(HttpListenerContext context, HttpStatusCode httpStatusCode, string message)
        {
            context.Response.StatusCode = (int)httpStatusCode;
            context.Response.ContentEncoding = ResponseEncoding;

            byte[] data = ResponseEncoding.GetBytes(message);

            context.Response.Close(data, false);
        }

        /// <summary>
        /// Responds with HTTP 400 and the specified message.
        /// </summary>
        protected void ProcessBadRequest(HttpListenerContext context, string message)
        {
            Respond(context, HttpStatusCode.BadRequest, message);
        }
        
        protected void RespondForbidden(HttpListenerContext context)
        {
            Respond(context, HttpStatusCode.Forbidden, "Access denied.");
        }
        #endregion

        #region Static File Requests
        /// <summary>
        /// Writes the file specified in the request ListeningAddress into the response stream.
        /// </summary>
        protected virtual void ProcessFileRequest(HttpListenerContext context)
        {
            string filePath = GetServerFilePath(context);

            try
            {
                ServeFile(context, filePath);
            }
            catch (FileNotFoundException)
            {
                RespondFileNotFound(context);
            }
            catch (DirectoryNotFoundException)
            {
                RespondFileNotFound(context);
            }
            catch (UnauthorizedAccessException)
            {
                RespondForbidden(context);
            }
        }

        protected string GetWebSitePath(HttpListenerContext context)
        {
            Debug.Assert(context.Request.Url.AbsolutePath.StartsWith(WebSiteUrlBase));

            string url = context.Request.Url.AbsolutePath.Substring(WebSiteUrlBase.Length);
            
            return url;
        }

        protected string GetServerFilePath(HttpListenerContext context)
        {
            return Path.Combine(
                WebSiteRoot.FullName,
                GetWebSitePath(context)
                );
        }

        /// <summary>
        /// Serves a file from the WebUI directory or a 404 message.
        /// </summary>
        protected void ServeFile(HttpListenerContext context, string path)
        {
            using (FileStream data = File.OpenRead(path))
            {
                string ext = Path.GetExtension(path);
                context.Response.ContentType = MimeTypes.ExtensionLookup(ext);

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
        protected void RespondFileNotFound(HttpListenerContext context)
        {
            Respond(context, HttpStatusCode.NotFound, "404 Not Found.");
        }
        #endregion
        #endregion
        
        #region Trace Helpers
        [Conditional("TRACE")]
        protected static void TraceHttpRequest(HttpListenerContext context)
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
        protected static void TraceHttpResponse(HttpListenerContext context)
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
        protected static void TraceWriteLine(string message)
        {
            Trace.WriteLine(message);
        }

        [Conditional("TRACE")]
        protected static void TraceWriteLine(string format, params object[] args)
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
