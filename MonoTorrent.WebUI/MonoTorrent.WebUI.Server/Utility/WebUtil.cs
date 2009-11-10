using System;
using System.Text;
using MonoTorrent.Client;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Common;

namespace MonoTorrent.WebUI.Server.Utility
{
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

    /// <summary>
    /// Utility class for converting values between WebUI and MonoTorrent.
    /// </summary>
    static class WebUtil
    {
        #region Converters
        /// <summary>
        /// Converts MonoTorrent's torrent state into WebUI state.
        /// </summary>
        public static WebState StateAdapter(MonoTorrent.Common.TorrentState state)
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
        public static MonoTorrent.Common.Priority PriorityAdapter(WebPriority priority)
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
        public static WebPriority PriorityAdapter(MonoTorrent.Common.Priority priority)
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
        public static WebOption BooleanAdapter(bool? value)
        {
            if (value.HasValue)
                return value.Value ? WebOption.Enabled : WebOption.Disabled;
            else
                return WebOption.NotAllowed;
        }

        /// <summary>
        /// Converts a nullable boolean to WebUI option value.
        /// </summary>
        public static bool? BooleanAdapter(WebOption value)
        {
            switch (value)
            {
                case WebOption.Enabled: return true;
                case WebOption.Disabled: return false;
                case WebOption.NotAllowed: return null;
                default: throw new InvalidOperationException();
            }
        } 
        #endregion

        /// <summary>
        /// Helper method to constructs a string to represent the list of a torrent's trackers.
        /// </summary>
        public static string GetTrackerString(TorrentManager torrent)
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
    }
}
