using System;
using System.Diagnostics;
using MonoTorrent.WebUI.Common;
using MonoTorrent.WebUI.Configuration;
using MonoTorrent.WebUI.Server.Configuration;

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
        private HttpServerWebUI httpServer = null;
        
        /// <summary>
        /// BitTorrent client node controller.
        /// </summary>
        private ITorrentController torrents;

        /// <summary>
        /// Initializes a service which will expose a WebUI for the <paramref name="monoTorrentClient"/> instance.
        /// </summary>
        /// <param name="monoTorrentClient">BitTorrent client to be exposed by WebUI.</param>
        public ServiceWebUI(ITorrentController torrents)
            : base(WebUISection.SectionName)
        {
            if (torrents == null)
                throw new ArgumentNullException("monoTorrentClient");
            
            this.torrents = torrents;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // 
            // ServiceWebUI
            // 
            this.ServiceName = "MonoTorrentWebUI";

        }

        #region Service Control Manager API

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);

            httpServer = new HttpServerWebUI();
            
            httpServer.BuildNumber = Config.BuildNumber;
            httpServer.ResponseEncoding = Config.ResponseEncoding;

            httpServer.StartHttpServer(
                this.torrents,
                base.Config
                );
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
                httpServer.Dispose();

            base.Dispose(disposing);
        }
    }
}
