using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Diagnostics;
using System.Configuration;
using System.ServiceProcess;
//using System.Runtime.Remoting;
using System.Collections.Generic;
using System.Security.Permissions;
using MonoTorrent.Common;
using MonoTorrent.Client;
using MonoTorrent.Client.Encryption;

namespace MonoTorrent.ClientService
{
    /// <summary>
    /// Long-lived service which runs 
    /// </summary>
    /// <typeparam name="TID"></typeparam>
    [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.RemotingConfiguration)]
    public partial class MonoTorrentClient : ServiceBase//, MonoTorrent.Service.IServiceController<TID>
    {
        //ObjRef objRef = null;

        ClientEngine client;
        Dictionary<string, TorrentManager> torrents;

        string DataDirectory;
        IPEndPoint ListenEndpoint;
        int GlobalMaxConnections;
        int GlobalMaxHalfOpenConnections;
        int GlobalMaxDownloadSpeed;
        int GlobalMaxUploadSpeed;
        EncryptionTypes EncryptionFlags;

        public MonoTorrentClient()
        {
            InitializeComponent();

            torrents = new Dictionary<string, TorrentManager>();

            LoadAppSettings();
            
            EngineSettings settings = new EngineSettings(
                DataDirectory, 
                ListenEndpoint.Port, 
                GlobalMaxConnections, 
                GlobalMaxHalfOpenConnections,
                GlobalMaxDownloadSpeed,
                GlobalMaxUploadSpeed,
                EncryptionFlags);

            client = new ClientEngine(settings);
        }

        private void LoadAppSettings()
        {
            DataDirectory = ConfigurationManager.AppSettings["SaveDirectory"];
            
            IPAddress listenAddress = IPAddress.Parse(ConfigurationManager.AppSettings["ListenAddress"]);
            int listenPort = int.Parse(ConfigurationManager.AppSettings["ListenPort"]);
            ListenEndpoint = new IPEndPoint(listenAddress, listenPort);

            GlobalMaxConnections = int.Parse(ConfigurationManager.AppSettings["GlobalMaxConnections"]);
            GlobalMaxHalfOpenConnections = int.Parse(ConfigurationManager.AppSettings["GlobalMaxHalfOpenConnections"]);
            GlobalMaxDownloadSpeed = int.Parse(ConfigurationManager.AppSettings["GlobalMaxDownloadSpeed"]);
            GlobalMaxUploadSpeed = int.Parse(ConfigurationManager.AppSettings["GlobalMaxUploadSpeed"]);
            EncryptionFlags = (EncryptionTypes)int.Parse(ConfigurationManager.AppSettings["EncryptionFlags"]);
        }

        private void ApplyAppSettings()
        {
            client.Settings.SavePath = DataDirectory;

            if(!client.Listener.Endpoint.Equals(ListenEndpoint))
                client.ChangeListenEndpoint(ListenEndpoint);
            
            client.Settings.GlobalMaxConnections = GlobalMaxConnections;
            client.Settings.GlobalMaxHalfOpenConnections = GlobalMaxHalfOpenConnections;
            client.Settings.GlobalMaxDownloadSpeed = GlobalMaxDownloadSpeed;
            client.Settings.GlobalMaxUploadSpeed = GlobalMaxUploadSpeed;
            client.Settings.AllowedEncryption = EncryptionFlags;
        }

        #region Service Control
        protected override void OnStart(string[] args)
        {
            //string configFile = Path.Combine(
            //    AppDomain.CurrentDomain.BaseDirectory, "MonoTorrent.ClientService.exe.config"
            //    );

            //RemotingConfiguration.Configure(configFile, false);
            //objRef = RemotingServices.Marshal(this, "MonoTorrentClientService.rem");
        }

        List<TorrentManager> massPaused = new List<TorrentManager>();
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
            //if (objRef != null)
            //{
            //    RemotingServices.Unmarshal(objRef);
            //    objRef = null;
            //}

            StopAll();
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

        #region IServiceController Members

        public TorrentManager AddTorrent(Torrent torrent, string torrentInstanceID, string savePath, string baseDirectory,
            int uploadSlots, 
            int maxConnections, 
            int maxDownloadSpeed, 
            int maxUploadSpeed, 
            bool initialSeedingEnabled)
        {
            #region Validation
            if (savePath != null && !savePath.StartsWith(DataDirectory))
                throw new ArgumentException("Save path must descend from the base directory.", "savePath");

            if (baseDirectory != null && baseDirectory.IndexOfAny(Path.GetInvalidPathChars()) > -1)
                throw new ArgumentException("Base directory contains invalid characters.", "baseDirectory"); 
            #endregion

            TorrentSettings torrentSettings = new TorrentSettings(
                uploadSlots, 
                maxConnections, 
                maxDownloadSpeed, 
                maxUploadSpeed, 
                initialSeedingEnabled
                );
            
            TorrentManager mgr;
            if(String.IsNullOrEmpty(baseDirectory))
                mgr = new TorrentManager(torrent, savePath ?? DataDirectory, torrentSettings);
            else
                mgr = new TorrentManager(torrent, savePath ?? DataDirectory, torrentSettings, baseDirectory);

            client.Register(mgr);

            torrents.Add(torrentInstanceID, mgr);

            WriteTrace("Torrent \"{0}\" added.", torrent.Name);

            return mgr;
        }

        public TorrentManager AddTorrent(Stream metaData, string savePath, string baseDirectory, int uploadSlots, int maxConnections, int maxDownloadSpeed, int maxUploadSpeed, bool initialSeedingEnabled)
        {
            Torrent torrent = Torrent.Load(metaData);

            //TODO: Implementation specific... fix it!
            string torrentInstanceID = (string)Convert.ChangeType(Toolbox.ToHex(torrent.InfoHash), typeof(string));

            return AddTorrent(torrent,
                torrentInstanceID,
                savePath, baseDirectory,
                uploadSlots, maxConnections, maxDownloadSpeed, maxUploadSpeed,
                initialSeedingEnabled);
        }

        public void RemoveTorrent(string torrentInstance, bool removeData)
        {
            TorrentManager torrent;
            if (!torrents.TryGetValue(torrentInstance, out torrent))
                return;

            torrent.Stop().WaitOne();

            client.Unregister(torrent);
            torrents.Remove(torrentInstance);

            //if (removeData)
            //    Directory.Delete(torrent.SavePath);

            WriteTrace("Torrent \"{0}\" removed.", torrent.Torrent.Name);
        }

        public void Start(string torrentInstance)
        {
            TorrentManager torrent;
            if (!torrents.TryGetValue(torrentInstance, out torrent))
                return;

            torrent.Start();

            WriteTrace("Torrent \"{0}\" started.", torrent.Torrent.Name);
        }

        public void Stop(string torrentInstance)
        {
            TorrentManager torrent;
            if (!torrents.TryGetValue(torrentInstance, out torrent))
                return;

            torrent.Stop();

            WriteTrace("Torrent \"{0}\" stopped.", torrent.Torrent.Name);
        }

        public void Pause(string torrentInstance)
        {
            TorrentManager torrent;
            if (!torrents.TryGetValue(torrentInstance, out torrent))
                return;

            torrent.Pause();

            WriteTrace("Torrent \"{0}\" paused.", torrent.Torrent.Name);
        }

        //public TransferInfo<TID>[] GetTorrents()
        //{
        //    List<TransferInfo<TID>> transfers = new List<TransferInfo<TID>>(client.TorrentCount);

        //    foreach(KeyValuePair<TID, TorrentManager> pair in torrents)
        //    {
        //        TID id = pair.Key;
        //        TorrentManager mgr = pair.Value;
                
        //        TransferInfo<TID> info = new TransferInfo<TID>()
        //        {
        //            InstanceID = id,
        //            Name = mgr.Torrent.Name,
        //            Progress = mgr.Progress,
        //            Size  = 0,
        //            State = CalculateState(mgr),
        //            Seeds = mgr.Peers.Seeds,
        //            Peers = mgr.Peers.Leechs,
        //            DownloadRate = mgr.Monitor.DownloadSpeed,
        //            UploadRate = mgr.Monitor.UploadSpeed,
        //            DataBytesDownloaded = mgr.Monitor.DataBytesDownloaded,
        //            DataBytesUploaded = mgr.Monitor.DataBytesUploaded
        //        };

        //        transfers.Add(info);
        //    }

        //    WriteTrace("Torrent list of {0} contructed.", transfers.Count);

        //    return transfers.ToArray();
        //}

        //private TransferState CalculateState(TorrentManager mgr)
        //{
        //    switch(mgr.State)
        //    {
        //        case TorrentState.Downloading:  return TransferState.Downloading;
        //        case TorrentState.Seeding:      return TransferState.Seeding;
        //        case TorrentState.Paused:       return TransferState.Paused;
        //        case TorrentState.Stopped:      return mgr.Complete ? TransferState.Done : TransferState.Stopped;
        //        case TorrentState.Stopping:     return TransferState.Stopping;
        //        case TorrentState.Hashing:      return TransferState.Hashing;
        //        default:                        return TransferState.Unknown;
        //    }
        //}

        public void StopAll()
        {
            WaitHandle[] stopHandles = client.StopAll();
            
            if(stopHandles.Length > 0)
                WaitHandle.WaitAll(stopHandles);
        }

        public void PauseAll()
        {
            client.PauseAll();
        }

        public void ResumeAll()
        {
            foreach(TorrentManager mgr in torrents.Values)
            {
                if(mgr.State == TorrentState.Paused)
                    mgr.Start();
            }
        }

        public void SetFilePriority(string torrentInstanceID, int[] fileIndexes, Priority priority)
        {
            TorrentManager torrent;
            if (!torrents.TryGetValue(torrentInstanceID, out torrent))
                return;

            foreach (int i in fileIndexes)
            {
                torrent.FileManager.Files[i].Priority = priority;
            }

            WriteTrace("Torrent \"{0}\" priority set to {1}.", torrent.Torrent.Name, priority);
        }

        //public TransferInfo<TID> GetTorrentDetails(TID torrentInstanceID)
        //{
        //    TorrentManager mgr;
        //    if (!torrents.TryGetValue(torrentInstanceID, out mgr))
        //        return null;

        //    List<TransferFileInfo> files = new List<TransferFileInfo>(mgr.FileManager.Files.Length);

        //    foreach (TorrentFile file in mgr.FileManager.Files)
        //    {
        //        TransferFileInfo fileInfo = new TransferFileInfo() 
        //        {
        //            Path = file.Path,
        //            Length = file.Length,
        //            Priority = (TransferFilePriority)file.Priority
        //        };

        //        files.Add(fileInfo);
        //    }

        //    TransferInfo<TID> info = new TransferInfo<TID>()
        //    {
        //        InstanceID = torrentInstanceID,
        //        Name = mgr.Torrent.Name,
        //        Progress = mgr.Progress,
        //        Size = 0,
        //        State = CalculateState(mgr),
        //        Seeds = mgr.Peers.Seeds,
        //        Peers = mgr.Peers.Leechs,
        //        DownloadRate = mgr.Monitor.DownloadSpeed,
        //        UploadRate = mgr.Monitor.UploadSpeed,
        //        DataBytesDownloaded = mgr.Monitor.DataBytesDownloaded,
        //        DataBytesUploaded = mgr.Monitor.DataBytesUploaded,
        //        Files = files.ToArray()
        //    };

        //    return info;
        //}

        #endregion
        
        private static void WriteTrace(string format, params object[] args)
        {
            Trace.WriteLine(String.Format(format, args));
        }

        internal TorrentManager GetTorrentDetails(string torrentInstanceID)
        {
            TorrentManager mgr;
            if (!torrents.TryGetValue(torrentInstanceID, out mgr))
                return null;

            return mgr;
        }

        public int Count
        {
            get { return torrents.Count; }
        }

        public IEnumerator<KeyValuePair<string, TorrentManager>> GetTorrentEnumerator()
        {
            return torrents.GetEnumerator();
        }
    }
}
