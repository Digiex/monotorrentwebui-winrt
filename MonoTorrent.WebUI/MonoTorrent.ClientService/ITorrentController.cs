using System;
using System.IO;
using System.Collections.Generic;
using MonoTorrent.Common;
using MonoTorrent.Client;

namespace MonoTorrent.ClientService
{
    /// <summary>
    /// Interface for controlling torrents.
    /// Mainly used to cleanup code.
    /// </summary>
    public interface ITorrentController
    {
        /// <summary>
        /// Register the torrent with the MonoTorrent engine.
        /// </summary>
        /// <param name="torrentMetaData">Stream containing the .torrent file.</param>
        /// <param name="savePath">Directory where to save the torrent.</param>
        /// <param name="baseDirectory">Directory name for multi-file torrents or file name of the torrent.</param>
        /// <param name="uploadSlots">The maximum number of upload slots for this torrent.</param>
        /// <param name="maxConnections">The maxium number of connection for this torrent.</param>
        /// <param name="maxDownloadSpeed">The maximum download speed for this torrent.</param>
        /// <param name="maxUploadSpeed">The maximum upload speed for this torrent.</param>
        /// <param name="initialSeedingEnabled">True to enable "super-seeding".</param>
        /// <returns>TorrentManager responsible for the torrent.</returns>
        TorrentManager AddTorrent(Stream torrentMetaData,
            string savePath, 
            string baseDirectory,
            int uploadSlots,
            int maxConnections,
            int maxDownloadSpeed,
            int maxUploadSpeed,
            bool superSeed);

        /// <summary>
        /// Register the torrent with the MonoTorrent engine.
        /// </summary>
        /// <param name="torrentMetaData">Stream containing the .torrent file.</param>
        /// <param name="savePath">Directory where to save the torrent.</param>
        /// <param name="baseDirectory">Directory name for multi-file torrents or file name of the torrent.</param>
        TorrentManager AddTorrent(Stream torrentMetaData,
            string savePath,
            string baseDirectory);

        /// <summary>
        /// Starts the specified torrent.
        /// </summary>
        /// <param name="torrentID">Identifier of the torrent.</param>
        /// <returns>False when <paramref name="torrentID"/> is not registered, otherwise true.</returns>
        bool StartTorrent(string torrentID);

        /// <summary>
        /// Pauses the specified torrent.
        /// </summary>
        /// <param name="torrentID">Identifier of the torrent.</param>
        /// <returns>False when <paramref name="torrentID"/> is not registered, otherwise true.</returns>
        bool PauseTorrent(string torrentID);

        /// <summary>
        /// Stops the specified torrent.
        /// </summary>
        /// <param name="torrentID">Identifier of the torrent.</param>
        /// <returns>False when <paramref name="torrentID"/> is not registered, otherwise true.</returns>
        bool StopTorrent(string torrentID);

        /// <summary>
        /// Removes the specified torrent.
        /// </summary>
        /// <param name="torrentID">Identifier of the torrent.</param>
        /// <param name="removeData">True to also remove any downloaded data files.</param>
        /// <returns>False when <paramref name="torrentID"/> is not registered, otherwise true.</returns>
        bool RemoveTorrent(string torrentID, bool removeData);

        /// <summary>
        /// Stops all torrents.
        /// </summary>
        void StopAllTorrents();

        /// <summary>
        /// Pauses all torrents.
        /// </summary>
        void PauseAllTorrents();

        /// <summary>
        /// Starts all paused torrensts.
        /// </summary>
        void ResumeAllTorrents();

        /// <summary>
        /// Sets priority of the specified files within the specified torrent.
        /// </summary>
        /// <param name="torrentID">Identifier of the torrent.</param>
        /// <param name="fileIndexes">Indexes of files to which the priority will be assigned.</param>
        /// <param name="priority">Priority to assign to the specified files.</param>
        /// <returns>False when <paramref name="torrentID"/> is not registered, otherwise true.</returns>
        bool SetFilePriority(string torrentID, int[] fileIndexes, Priority priority);

        /// <summary>
        /// Retrieves the torrent manager based on the identifier string.
        /// The returned instance should be used as read-only, use provided API to control torrents.
        /// </summary>
        /// <param name="torrentID">Identifier of the torrent.</param>
        /// <returns>The instance corresponding to the <paramref name="torrentID"/>, otherwise null.</returns>
        TorrentManager GetTorrentManager(string torrentID);

        /// <summary>
        /// Sets the category label for the specified torrent.
        /// </summary>
        /// <param name="torrentID">Identifier of the torrent.</param>
        /// <param name="label">Category label to set.</param>
        /// <returns>False when <paramref name="torrentID"/> is not registered, otherwise true.</returns>
        bool SetTorrentLabel(string torrentID, string label);

        /// <summary>
        /// Returns a list of torrent labels and the number of torrents using that label.
        /// </summary>
        IEnumerable<KeyValuePair<string, int>> GetAllLabels();

        /// <summary>
        /// Number of registered torrents.
        /// </summary>
        int TorrentCount { get; }

        /// <summary>
        /// Enumerator for registered identifier:torrents pairs.
        /// TorrentManager should be treated as read-only, use the provided API to control torrents.
        /// </summary>
        IEnumerable<KeyValuePair<string, TorrentManager>> TorrentManagers { get; }
    }
}
