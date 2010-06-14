using System;
using System.Diagnostics;
using MonoTorrent.WebUI.Common;
using MonoTorrent.WebUI.Configuration;
using MonoTorrent.WebUI.Server.Configuration;
using MonoTorrent.Client;

namespace MonoTorrent.WebUI.Server
{
    /// <summary>
    /// Service implementation.
    /// </summary>
    public class ServiceWebUI : MonoTorrent.WebUI.Configuration.ConfiguredServiceBase<WebUISection>
    {
        /// <summary>
        /// WebUI HTTP server.
        /// </summary>
        private WebUIServer httpServer = null;
        
        /// <summary>
        /// BitTorrent client node controller.
        /// </summary>
        private ITorrentController<string, TorrentManager> torrents;

        /// <summary>
        /// Initializes a service which will expose a WebUI for the <paramref name="monoTorrentClient"/> instance.
        /// </summary>
        /// <param name="monoTorrentClient">BitTorrent client to be exposed by WebUI.</param>
        public ServiceWebUI(ITorrentController<string, TorrentManager> torrents)
            : base(WebUISection.SectionName)
        {
            if (torrents == null)
                throw new ArgumentNullException("monoTorrentClient");
            
            this.torrents = torrents;

            this.ServiceName = "MonoTorrentWebUI";
        }

        #region Service Control Manager API

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);

            httpServer = new WebUIServer(this.torrents);
            
            httpServer.StartHttpServer(base.Config);
        }

        public string ListeningAddress
        {
            get { return Config.HttpListenerPrefix; }
        }

        protected override void OnStop()
        {
            if (httpServer != null)
            {
                httpServer.StopHttpServer();
                httpServer.Dispose();
            }
            
            base.OnStop();
        }

        #region Debug Interface
        /// <summary>
        /// Calls the OnStart event with 
        /// </summary>
        [System.Diagnostics.Conditional("DEBUG")]
        public void DebugStart()
        {
            OnStart(new string[] { });
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public void DebugStop()
        {
            OnStop();
        }
        #endregion 

        #endregion

        #region Trace Helpers
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if(httpServer != null)
                    httpServer.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
