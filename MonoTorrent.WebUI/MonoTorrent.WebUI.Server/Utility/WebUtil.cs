using System;
using System.Text;
using MonoTorrent.Client;
using MonoTorrent.Client.Tracker;
using MonoTorrent.Common;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
// enumerable of (string:string) pairs
using KeyValueBag = System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, string>>;

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

        public static WebPriority ParseWebPriority(string value)
        {
            if (String.IsNullOrEmpty(value))
                throw new ApplicationException("Invalid file priority value.");

            int priority;
            int.TryParse(value, out priority);
            
            return (WebPriority)priority;
        }

        public static KeyValuePair<string, string>[] ParseParallelLists(HttpListenerRequest request, string field1, string field2)
        {
            string[] list1 = ParseList(request, field1);
            string[] list2 = ParseList(request, field2);

            int length = Math.Min(list1.Length, list2.Length);

            KeyValuePair<string, string>[] pairs = new KeyValuePair<string, string>[length];

            for (int i = 0; i < length; i++)
            {
                pairs[i] = new KeyValuePair<string, string>(list1[i], list2[i]);
            }

            return pairs;
        }

        public static string[] ParseList(HttpListenerRequest request, string fieldName)
        {
            string list = request.QueryString[fieldName];

            if (String.IsNullOrEmpty(list))
                return new string[] { };

            return list.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static readonly Regex cookieSplit = new Regex("^(?<url>.*)(:COOKIE:(?<cookies>(([^=]*=?([^;]*|;|$)))*))?", RegexOptions.Compiled);
        private static readonly Regex cookieParse = new Regex("^(?<key>[^=]+)(=(?<value>[^;]*))?$", RegexOptions.Compiled);

        /// <summary>
        /// Parses uTorrent's COOKIE query string parameter "http://host/path/:COOKIE:c1=v2;c2=v2..."
        /// </summary>
        /// <param name="url">Raw ListeningAddress of the torrent string.</param>
        /// <param name="uri">ListeningAddress without the COOKIE suffix.</param>
        /// <param name="cookies">List of parsed cookies.</param>
        private static void ParseCookieSuffix(string url, out Uri uri, out List<Cookie> cookies)
        {
            // Match url http://some-tracker.com/torrent/12345:COOKIE:abc=123;xyz=789
            Match match = cookieSplit.Match(url);

            if (!match.Success)
            {
                uri = null;
                cookies = null;
                return;
            }

            Uri.TryCreate(match.Result("${url}"), UriKind.Absolute, out uri);
            string rawCookies = match.Result("${cookies}");

            cookies = new List<Cookie>();
            string[] rawCookieList = rawCookies.Split(
                new char[] { ';' }, 
                StringSplitOptions.RemoveEmptyEntries
                );
            foreach (string rawCookie in rawCookieList)
            {
                var cookie = ParseQueryPair(rawCookie);

                cookies.Add(new Cookie(cookie.Key, cookie.Value));
            }
        }

        /// <summary>
        /// Fetches ListeningAddress after parsing out uTorrent style cookies suffix.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="maxSize">Maximum download size</param>
        public static byte[] FetchUrlWithCookies(string url, int maxSize)
        {
            Uri uri;
            List<Cookie> cookies;
            ParseCookieSuffix(url, out uri, out cookies);

            WebClientExt web = new WebClientExt();
            web.MaxDownloadSize = 524288; // 512KB
            if (cookies != null)
            {
                web.CookieContainer = new CookieContainer();

                foreach (var cookie in cookies)
                    web.CookieContainer.Add(uri, cookie);
            }
            
            return web.DownloadData(uri);
        }

        public static IEnumerable<KeyValuePair<string, KeyValueBag>> ParsePropertyChanges(string queryString)
        {
            string[] parts = queryString.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);

            return WalkQueryParts(parts);
        }

        private static IEnumerable<KeyValuePair<string, KeyValueBag>> WalkQueryParts(IEnumerable<string> parts)
        {
            IEnumerator<string> cursor = parts.GetEnumerator();
            
            if (!cursor.MoveNext())
                yield break;

            do
            {
                var pair = ParseQueryPair(cursor.Current);

                if (pair.Key == "hash")
                {
                    KeyValueBag bag = YieldHashPairs(cursor);

                    yield return new KeyValuePair<string, KeyValueBag>(pair.Value, bag);
                }
            }
            while (cursor.MoveNext());
        }

        private static KeyValueBag YieldHashPairs(IEnumerator<string> cursor)
        {
            while (cursor.MoveNext())
            {
                var pair = ParseQueryPair(cursor.Current);

                if (pair.Key == "hash")
                    yield break;
                else
                    yield return pair;
            }
        }

        private static KeyValuePair<string, string> ParseQueryPair(string input)
        {
            string[] parts = input.Split('=');

            string key = null;
            string value = null;

            key = parts[0];

            if (parts.Length == 2)
                value = parts[1];

            return new KeyValuePair<string, string>(key, value);
        }

        public static bool TestBooleanField(HttpListenerRequest Request, string field)
        {
            return String.Equals(Request.QueryString[field], "1", StringComparison.Ordinal);
        }
    }
}
