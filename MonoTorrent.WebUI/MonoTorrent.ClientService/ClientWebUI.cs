using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using MonoTorrent.Client;
using MonoTorrent.Common;
using MonoTorrent.Client.Tracker;
using MonoTorrent.ClientService.Configuration;
using Newtonsoft.Json;

namespace MonoTorrent.ClientService
{
    /// <summary>
    /// Service implementation.
    /// </summary>kk
    public partial class ClientWebUI : ConfiguredServiceBase<WebUISection>
    {
        /// <summary>
        /// Maps configuration between WebUI and MonoTorrent.
        /// </summary>
        SettingsAdapter settingsAdapter;

        /// <summary>
        /// Reference to the MonoTorrent engine.
        /// </summary>
        private ITorrentController torrents;

        /// <summary>
        /// Initializes a service which will expose a WebUI for the <paramref name="monoTorrentClient"/> instance.
        /// </summary>
        /// <param name="monoTorrentClient">An instance of the MonoTorrent client service.</param>
        public ClientWebUI(MonoTorrentClient monoTorrentClient)
            : base(WebUISection.SectionName)
        {
            if (monoTorrentClient == null)
                throw new ArgumentNullException("monoTorrentClient");
            
            this.ServiceName = "MonoTorrent WebUI Server";
            this.CanPauseAndContinue = true;

            this.torrents = monoTorrentClient;

            this.settingsAdapter = new SettingsAdapter(this, monoTorrentClient);

            InitializeHttpServer();
        }

        #region Service Control Manager API

        protected override void OnStart(string[] args)
        {
            base.OnStart(args);

            StartHttpServer();
        }

        protected override void OnStop()
        {
            StopHttpServer();
            
            base.OnStop();
        }

        protected override void OnPause()
        {
            PauseHttpServer();

            base.OnPause();
        }

        protected override void OnContinue()
        {
            base.OnContinue();

            ResumeHttpServer();
        }

        #region Debug methods
        [System.Diagnostics.Conditional("DEBUG")]
        public void SynthStart()
        {
            OnStart(new string[] { });
        }

        [System.Diagnostics.Conditional("DEBUG")]
        public void SynthStop()
        {
            OnStop();
        }
        #endregion 

        #endregion

        #region WebUI --> ITorrentController Adapter Methods

        // These functions call the appropriate methods of ITorrentController
        // and print the JSON response data.
        // All calls come from ProcessQueryRequest(...).

        /// <summary>
        /// Sets the setting values.
        /// </summary>
        private void SetSettings(string[] names, string[] values)
        {
            for (int i = 0; i < names.Length && i < values.Length; i++)
            {
                settingsAdapter[names[i]] = values[i];
            }
        }

        /// <summary>
        /// Prints the array of settings.
        /// </summary>
        private void PrintSettings(JsonWriter writer)
        {
            writer.WritePropertyName("settings");
            writer.WriteStartArray();

            settingsAdapter.PrintSettings(writer);

            writer.WriteEndArray();
        }

        /// <summary>
        /// Outputs the corresponding torrent's properties.
        /// </summary>
        private void PrintProperties(JsonWriter writer, string hash)
        {
            TorrentManager details = torrents.GetTorrentManager(hash);

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

        /// <summary>
        /// Helper method to constructs a string to represent the list of a torrent's trackers.
        /// </summary>
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
            TorrentManager details = torrents.GetTorrentManager(hash);

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

            foreach (KeyValuePair<string, TorrentManager> pair in torrents.TorrentManagers)
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
            torrents.AddTorrent(fileData, null, null);
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

            torrents.SetFilePriority(hash,
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
                torrents.StartTorrent(hash);
            }
        }

        /// <summary>
        /// Pauses torrents specified in the comma separated list of hashes.
        /// </summary>
        private void PauseTorrents(string hashes)
        {
            string[] hashList = hashes.Split(new char[] { ',' });

            foreach (string hash in hashList)
            {
                torrents.PauseTorrent(hash);
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
                torrents.StopTorrent(hash);
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
                torrents.RemoveTorrent(hash, removeData);
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
            torrents.SetTorrentLabel(hash, label);
        }

        #region Conversion Helpers
        /// <summary>
        /// Converts MonoTorrent's torrent state into WebUI state.
        /// </summary>
        private WebState StateAdapter(MonoTorrent.Common.TorrentState state)
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
        private MonoTorrent.Common.Priority PriorityAdapter(WebPriority priority)
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
        private WebPriority PriorityAdapter(MonoTorrent.Common.Priority priority)
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
        private WebOption BooleanAdapter(bool? value)
        {
            if (value.HasValue)
                return value.Value ? WebOption.Enabled : WebOption.Disabled;
            else
                return WebOption.NotAllowed;
        }

        /// <summary>
        /// Converts a nullable boolean to WebUI option value.
        /// </summary>
        private bool? BooleanAdapter(WebOption value)
        {
            switch (value)
            {
                case WebOption.Enabled: return true;
                case WebOption.Disabled: return false;
                case WebOption.NotAllowed: return null;
                default: throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// WebUI torrent state values.
        /// </summary>
        enum WebState : int
        {
            Active = 201,
            Stopped = 136,
            Queued = 200,
            Paused = 233
        }

        /// <summary>
        /// WebUI file priority values.
        /// </summary>
        enum WebPriority : int
        {
            Skip = 0,
            Low = 1,
            Normal = 2,
            High = 3
        }

        /// <summary>
        /// WebUI option value.
        /// </summary>
        enum WebOption : int
        {
            NotAllowed = -1,
            Disabled = 0,
            Enabled = 1
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
    }
}
