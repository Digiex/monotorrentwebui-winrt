using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using MonoTorrent.Client;
using MonoTorrent.ClientService.Configuration;
using MonoTorrent.Common;
using MonoTorrent.WebUI.Common;
using MonoTorrent.WebUI.Configuration;

namespace MonoTorrent.ClientService
{
    /// <summary>
    /// Service running the BitTorrent client, has an API for controlling torrents.
    /// </summary>
    public class MonoTorrentClient : ConfiguredServiceBase<ClientSection>, ITorrentController
    {
        /// <summary>
        /// MonoTorrent client engine.
        /// </summary>
        private ClientEngine client;

        /// <summary>
        /// Mapping between string identifiers and TorrentManager instances.
        /// </summary>
        private Dictionary<string, TorrentManager> torrents;

        /// <summary>
        /// Category labels for torrents.
        /// </summary>
        private Dictionary<string, string> torrentLabels;

        /// <summary>
        /// Creates a new instance of MonoTorrentClient.
        /// </summary>
        public MonoTorrentClient() 
            : base(ClientSection.SectionName)
        {
            this.CanPauseAndContinue = false;
            this.ServiceName = "MonoTorrent Client";

            torrents = new Dictionary<string, TorrentManager>();
            torrentLabels = new Dictionary<string, string>();

            // Settings are applied later when the service is started.
            EngineSettings defaultSettings = new EngineSettings();
            client = new ClientEngine(defaultSettings);
            client.Listener.Stop();
            client.DhtEngine.Stop();

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // 
            // MonoTorrentClient
            // 
            this.ServiceName = "MonoTorrentClient";

        }

        #region Configuration

        /// <summary>
        /// Applies the currently loaded configuration section.
        /// </summary>
        protected override void OnApplyConfiguration()
        {
            client.Settings.SavePath = Config.SavePath.ToString();

            if (!client.Listener.Endpoint.Equals(Config.ListenEndPoint))
                client.ChangeListenEndpoint(Config.ListenEndPoint);

            client.Settings.GlobalMaxConnections = Config.MaxGlobalConnections;
            client.Settings.GlobalMaxHalfOpenConnections = Config.MaxHalfOpenConnections;
            client.Settings.GlobalMaxDownloadSpeed = Config.MaxDownloadRate;
            client.Settings.GlobalMaxUploadSpeed = Config.MaxUploadRate;
            client.Settings.AllowedEncryption = Config.AllowedEncryption;
        }

        /// <summary>
        /// Propagates current settings into the configuration section.
        /// </summary>
        protected override void OnCollectConfiguration()
        {
            Config.SavePath = new DirectoryInfo(client.Settings.SavePath);
            
            Config.ListenEndPoint = client.Listener.Endpoint;

            Config.MaxGlobalConnections = client.Settings.GlobalMaxConnections;
            Config.MaxHalfOpenConnections = client.Settings.GlobalMaxHalfOpenConnections;
            Config.MaxDownloadRate = client.Settings.GlobalMaxDownloadSpeed;
            Config.MaxUploadRate = client.Settings.GlobalMaxUploadSpeed;
            Config.AllowedEncryption = client.Settings.AllowedEncryption;
        }
        #endregion

        #region Service Control Interface
        protected override void OnStart(string[] args)
        {
            base.OnStart(args); // configuration gets loaded

            // TODO: Load torrents
            
            client.Listener.Start();
            client.DhtEngine.Start();
        }

        protected override void OnStop()
        {
            StopAllTorrents();

            client.Listener.Stop();
            client.DhtEngine.Stop();
            client.DiskManager.Flush();

            base.OnStop();
        }

        #region Debug Interface
        [Conditional("DEBUG")]
        public void DebugStart()
        {
            OnStart(new string[] { });
        }

        [Conditional("DEBUG")]
        public void DebugStop()
        {
            OnStop();
        }
        #endregion

        #endregion

        #region ITorrentController Members

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

        public TorrentManager AddTorrent(byte[] torrentMetaData, string savePath, string baseDirectory)
        {
            return AddTorrent(torrentMetaData, savePath, baseDirectory,
                Config.DefaultUploadSlots,
                Config.DefaultMaxConnections,
                Config.DefaultDownloadRate,
                Config.DefaultUploadRate,
                false);
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
        public TorrentManager AddTorrent(byte[] torrentMetaData, string savePath, string baseDirectory, 
            int uploadSlots, 
            int maxConnections, 
            int maxDownloadSpeed, 
            int maxUploadSpeed, 
            bool superSeed)
        {
            Torrent torrent = Torrent.Load(torrentMetaData);

            string torrentID = torrent.InfoHash.ToHex();
            
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

            torrent.Stop();

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

            torrent.Stop();

            client.Unregister(torrent);
            torrents.Remove(torrentInfoHash);

            //if (removeData)
            //    Directory.Delete(torrent.SavePath);
            
            WriteTrace("Torrent \"{0}\" removed.", torrent.Torrent.Name);

            return true;
        }

        /// <summary>
        /// Recheck the specified torrent's data.
        /// </summary>
        /// <param name="torrentInfoHash">Identifier of the torrent.</param>
        /// <returns>False when <paramref name="torrentInfoHash"/> is not registered, otherwise true.</returns>
        public bool RecheckTorrentData(string torrentInfoHash)
        {
            TorrentManager torrent;
            if (!torrents.TryGetValue(torrentInfoHash, out torrent))
                return false;

            torrent.HashCheck(true);

            return true;
        }
        
        /// <summary>
        /// Stops all torrents.
        /// </summary>
        public void StopAllTorrents()
        {
            client.StopAll();
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
                torrent.Torrent.Files[i].Priority = priority;
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

        /// <summary>
        /// Sets the category label for the specified torrent.
        /// </summary>
        /// <param name="torrentInfoHash">Identifier of the torrent.</param>
        /// <param name="label">Category label to set.</param>
        /// <returns>False when <paramref name="torrentInfoHash"/> is not registered, otherwise true.</returns>
        public bool SetTorrentLabel(string torrentInfoHash, string label)
        {
            if (torrents.ContainsKey(torrentInfoHash))
            {
                torrentLabels[torrentInfoHash] = label;
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns enumeration of distinct labels.
        /// </summary>
        public IEnumerable<KeyValuePair<string, int>> GetAllLabels()
        {
            Dictionary<string, int> labels = new Dictionary<string,int>();

            foreach (string label in torrentLabels.Values)
            {
                if (!String.IsNullOrEmpty(label))
                {
                    if (labels.ContainsKey(label))
                        labels[label]++;
                    else
                        labels.Add(label, 1);
                }
            }

            return labels;
        }

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

        public int MaxDownloadRate
        {
            get { return client.Settings.GlobalMaxDownloadSpeed; }
            set { client.Settings.GlobalMaxDownloadSpeed = value; }
        }
        #endregion

        #region Trace Methods
        private static void WriteTrace(string format, params object[] args)
        {
            Trace.WriteLine(String.Format(format, args));
        } 
        #endregion
    }
}
