using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Diagnostics;
using System.Configuration;
using System.ServiceProcess;
using System.Collections.Generic;
using MonoTorrent.Common;
using MonoTorrent.Client;
using MonoTorrent.Client.Encryption;
using MonoTorrent.ClientService.Configuration;

namespace MonoTorrent.ClientService
{
    /// <summary>
    /// Service which runs the MonoTorrent engine and provides an interface to control torrents.
    /// </summary>
    public partial class MonoTorrentClient : ServiceBase
    {
        /// <summary>
        /// MonoTorrent client engine.
        /// </summary>
        private ClientEngine client;

        /// <summary>
        /// Mapping between string identifiers and TorrentManager instances.
        /// </summary>
        private Dictionary<string, TorrentManager> torrents;

        private MonoTorrentClientSection config;

        /// <summary>
        /// Creates a new instance of MonoTorrentClient.
        /// </summary>
        public MonoTorrentClient()
        {
            InitializeComponent();

            torrents = new Dictionary<string, TorrentManager>();

            // Settings are applied later when the service is started.
            EngineSettings defaultSettings = new EngineSettings();
            client = new ClientEngine(defaultSettings);
            client.Listener.Stop();
            client.DhtEngine.Stop();
        }

        #region Settings

        /// <summary>
        /// 
        /// </summary>
        /// <returns>EngineSettings instance.</returns>
        protected EngineSettings GetClientEngineSettings()
        {
            EngineSettings settings = new EngineSettings(
                config.SavePath.ToString(),
                config.ListenPort,
                config.MaxGlobalConnections,
                config.MaxHalfOpenConnections,
                config.MaxDownloadRate,
                config.MaxUploadRate,
                config.EncryptionFlags);

            return settings;
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

                config = (MonoTorrentClientSection)configFile.GetSection("MonoTorrentClient");
            }
            else
                config = (MonoTorrentClientSection)ConfigurationManager.GetSection("MonoTorrentClient");
        }

        /// <summary>
        /// Applies the currently loaded configuration section.
        /// </summary>
        private void ApplyConfiguration()
        {			
            client.Settings.SavePath = config.SavePath.ToString();

            if (!client.Listener.Endpoint.Equals(config.ListenEndPoint))
                client.ChangeListenEndpoint(config.ListenEndPoint);

            client.Settings.GlobalMaxConnections = config.MaxGlobalConnections;
            client.Settings.GlobalMaxHalfOpenConnections = config.MaxHalfOpenConnections;
            client.Settings.GlobalMaxDownloadSpeed = config.MaxDownloadRate;
            client.Settings.GlobalMaxUploadSpeed = config.MaxUploadRate;
            client.Settings.AllowedEncryption = config.EncryptionFlags;
        }
        #endregion

        #region Service Control Interface
        protected override void OnStart(string[] args)
        {
            LoadConfiguration(args);
            ApplyConfiguration();
            // TODO: Load torrents

            client.Listener.Start();
            client.DhtEngine.Start();
        }

        /// <summary>
        /// List of torrents which were paused by OnPause().
        /// Stored so that they can be resumed in OnContinue().
        /// </summary>
        private List<TorrentManager> massPaused = new List<TorrentManager>();

        protected override void OnPause()
        {
            massPaused.Clear();

            foreach (TorrentManager torrent in torrents.Values)
            {
                if (torrent.State == TorrentState.Downloading || torrent.State == TorrentState.Seeding)
                    massPaused.Add(torrent);
            }

            client.PauseAll();
        }

        protected override void OnContinue()
        {
            foreach (TorrentManager torrent in massPaused)
            {
                torrent.Start();
            }
        }

        protected override void OnStop()
        {
            StopAllTorrents();

            client.Listener.Stop();
            client.DhtEngine.Stop();
        }

        #region Debug methods
        [Conditional("DEBUG")]
        public void StartService()
        {
            OnStart(new string[] { });
        }

        [Conditional("DEBUG")]
        public void StopService()
        {
            OnStop();
        }
        #endregion

        #endregion

        #region Torrent Control Interface

        private TorrentManager AddTorrent(Torrent torrent, string torrentID, string savePath, string baseDirectory,
            int uploadSlots, 
            int maxConnections, 
            int maxDownloadSpeed, 
            int maxUploadSpeed, 
            bool superSeed)
        {
            // TODO: Add security checks on savePath

            TorrentSettings torrentSettings = new TorrentSettings(
                uploadSlots, 
                maxConnections, 
                maxDownloadSpeed, 
                maxUploadSpeed, 
                superSeed
                );
            
            TorrentManager mgr;
            if(baseDirectory == null)
                mgr = new TorrentManager(torrent, savePath, torrentSettings);
            else
                mgr = new TorrentManager(torrent, savePath, torrentSettings, baseDirectory);

            client.Register(mgr);

            torrents.Add(torrentID, mgr);

            WriteTrace("Torrent \"{0}\" added.", torrent.Name);

            return mgr;
        }

        /// <summary>
        /// Register the torrent with the MonoTorrent engine.
        /// </summary>
        /// <param name="torrentMetaData">Stream containing the .torrent file.</param>
        /// <param name="savePath">Directory where to save the torrent.</param>
        /// <param name="baseDirectory">Overrides the default directory or file name of the torrent.</param>
        /// <param name="uploadSlots">The maximum number of upload slots for this torrent.</param>
        /// <param name="maxConnections">The maxium number of connection for this torrent.</param>
        /// <param name="maxDownloadSpeed">The maximum download speed for this torrent.</param>
        /// <param name="maxUploadSpeed">The maximum upload speed for this torrent.</param>
        /// <param name="initialSeedingEnabled">True to enable "super-seeding".</param>
        /// <returns>TorrentManager responsible for the torrent.</returns>
        public TorrentManager AddTorrent(Stream torrentMetaData, string savePath, string baseDirectory, 
            int uploadSlots, 
            int maxConnections, 
            int maxDownloadSpeed, 
            int maxUploadSpeed, 
            bool superSeed)
        {
            Torrent torrent = Torrent.Load(torrentMetaData);

            string torrentID = Toolbox.ToHex(torrent.InfoHash);
            
            if (savePath == null)
                savePath = client.Settings.SavePath;

            return AddTorrent(torrent,
                torrentID,
                savePath, baseDirectory,
                uploadSlots, maxConnections, maxDownloadSpeed, maxUploadSpeed,
                superSeed);
        }

        /// <summary>
        /// Starts the specified torrent.
        /// </summary>
        /// <param name="torrentInfoHash">Identifier of the torrent.</param>
        /// <returns>False when <paramref name="torrentInfoHash"/> is not registered, otherwise true.</returns>
        public bool StartTorrent(string torrentInfoHash)
        {
            TorrentManager torrent;
            if (!torrents.TryGetValue(torrentInfoHash, out torrent))
                return false;

            torrent.Start();

            WriteTrace("Torrent \"{0}\" started.", torrent.Torrent.Name);

            return true;
        }

        /// <summary>
        /// Pauses the specified torrent.
        /// </summary>
        /// <param name="torrentInfoHash">Identifier of the torrent.</param>
        /// <returns>False when <paramref name="torrentInfoHash"/> is not registered, otherwise true.</returns>
        public bool PauseTorrent(string torrentInfoHash)
        {
            TorrentManager torrent;
            if (!torrents.TryGetValue(torrentInfoHash, out torrent))
                return false;

            torrent.Pause();

            WriteTrace("Torrent \"{0}\" paused.", torrent.Torrent.Name);

            return true;
        }

        /// <summary>
        /// Stops the specified torrent.
        /// </summary>
        /// <param name="torrentInfoHash">Identifier of the torrent.</param>
        /// <returns>False when <paramref name="torrentInfoHash"/> is not registered, otherwise true.</returns>
        public bool StopTorrent(string torrentInfoHash)
        {
            TorrentManager torrent;
            if (!torrents.TryGetValue(torrentInfoHash, out torrent))
                return false;

            torrent.Stop().WaitOne();

            WriteTrace("Torrent \"{0}\" stopped.", torrent.Torrent.Name);

            return true;
        }

        /// <summary>
        /// Removes the specified torrent.
        /// </summary>
        /// <param name="torrentInfoHash">Identifier of the torrent.</param>
        /// <param name="removeData">True to also remove any downloaded data files.</param>
        /// <returns>False when <paramref name="torrentInfoHash"/> is not registered, otherwise true.</returns>
        public bool RemoveTorrent(string torrentInfoHash, bool removeData)
        {
            TorrentManager torrent;
            if (!torrents.TryGetValue(torrentInfoHash, out torrent))
                return false;

            torrent.Stop().WaitOne();

            client.Unregister(torrent);
            torrents.Remove(torrentInfoHash);

            //if (removeData)
            //    Directory.Delete(torrent.SavePath);
            
            WriteTrace("Torrent \"{0}\" removed.", torrent.Torrent.Name);

            return true;
        }
        
        /// <summary>
        /// Stops all torrents.
        /// </summary>
        public void StopAllTorrents()
        {
            WaitHandle[] stopHandles = client.StopAll();
            
            if(stopHandles.Length > 0)
                WaitHandle.WaitAll(stopHandles);
        }

        /// <summary>
        /// Pauses all torrents.
        /// </summary>
        public void PauseAllTorrents()
        {
            client.PauseAll();
        }

        /// <summary>
        /// Starts all paused torrensts.
        /// </summary>
        public void ResumeAllTorrents()
        {
            foreach(TorrentManager mgr in torrents.Values)
            {
                if(mgr.State == TorrentState.Paused)
                    mgr.Start();
            }
        }

        /// <summary>
        /// Sets priority of the specified files within the specified torrent.
        /// </summary>
        /// <param name="torrentInfoHash">Identifier of the torrent.</param>
        /// <param name="fileIndexes">Indexes of files to which the priority will be assigned.</param>
        /// <param name="priority">Priority to assign to the specified files.</param>
        /// <returns>False when <paramref name="torrentInfoHash"/> is not registered, otherwise true.</returns>
        public bool SetFilePriority(string torrentInfoHash, int[] fileIndexes, Priority priority)
        {
            TorrentManager torrent;
            if (!torrents.TryGetValue(torrentInfoHash, out torrent))
                return false;

            foreach (int i in fileIndexes)
            {
                torrent.FileManager.Files[i].Priority = priority;
            }

            WriteTrace("Torrent \"{0}\" priority set to {1}.", torrent.Torrent.Name, priority);

            return true;
        }

        /// <summary>
        /// Retrieves the torrent manager based on the identifier string.
        /// The returned instance should be used as read-only, use provided API to control torrents.
        /// </summary>
        /// <param name="torrentInfoHash">Identifier of the torrent.</param>
        /// <returns>The instance corresponding to the <paramref name="torrentInfoHash"/>, otherwise null.</returns>
        public TorrentManager GetTorrentManager(string torrentInfoHash)
        {
            TorrentManager mgr;
            if (!torrents.TryGetValue(torrentInfoHash, out mgr))
                return null;

            return mgr;
        }

        #endregion

        #region Trace Methods
        private static void WriteTrace(string format, params object[] args)
        {
            Trace.WriteLine(String.Format(format, args));
        } 
        #endregion

        #region Miscellaneous
        /// <summary>
        /// Number of registered torrents.
        /// </summary>
        public int TorrentCount
        {
            get { return torrents.Count; }
        }

        /// <summary>
        /// Enumerator for registered identifier:torrents pairs.
        /// TorrentManager should be treated as read-only, use the provided API to control torrents.
        /// </summary>
        public IEnumerable<KeyValuePair<string, TorrentManager>> TorrentManagers
        {
            get { return torrents; }
        } 
        #endregion
    }
}
