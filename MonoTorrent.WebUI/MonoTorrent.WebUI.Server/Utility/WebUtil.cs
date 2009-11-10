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

        private static readonly Regex cookieSplit = new Regex("^(?<url>.*):COOKIE:(?<cookies>(([^=]*=?([^;]*|;|$)))*)", RegexOptions.Compiled);
        private static readonly Regex cookieParse = new Regex("^(?<key>[^=]+)(=(?<value>[^;]*))?$", RegexOptions.Compiled);

        public static void ParseCookieSuffix(string url, out Uri uri, out List<Cookie> cookies)
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
            foreach (string rawCookie in rawCookies.Split(";".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
            {
                Match matchCookie = cookieParse.Match(rawCookie);

                string key = matchCookie.Result("${key}");
                string value = matchCookie.Result("${value}");
                cookies.Add(new Cookie(key, value));
            }
        }

        public static Stream FetchUrlWithCookies(string url, int maxSize)
        {
            Uri uri;
            List<Cookie> cookies;
            WebUtil.ParseCookieSuffix(url, out uri, out cookies);

            HttpWebRequest req = WebRequest.Create(uri) as HttpWebRequest;

            if (req == null)
                throw new NotSupportedException("Torrents may only be fetched from HTTP URIs.");

            if (cookies != null)
            {
                req.CookieContainer = new CookieContainer();

                foreach (var cookie in cookies)
                    req.CookieContainer.Add(uri, cookie);
            }

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            {
                return ReadResponse(resp, maxSize);
            }
        }

        private static Stream ReadResponse(HttpWebResponse resp, int maxSize)
        {
            int bufferSize = Math.Max((int)resp.ContentLength, 102400); // min 100KB

            MemoryStream bufferStream = new MemoryStream(bufferSize);
            using (Stream input = resp.GetResponseStream())
            {
                byte[] buffer = new byte[1024];
                int count;
                long total = 0;

                while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    bufferStream.Write(buffer, 0, count);
                    total += count;

                    if (total > maxSize)
                        throw new ApplicationException("Torrent file size is too large.");
                }
            }

            bufferStream.Seek(0, SeekOrigin.Begin);
            return bufferStream;
        }

        public static IEnumerable<KeyValuePair<string, KeyValueBag>> ParsePropChanges(HttpListenerRequest Request)
        {
            throw new NotImplementedException();
        }

        public static bool TestBooleanField(HttpListenerRequest Request, string field)
        {
            return String.Equals(Request.QueryString[field], "1", StringComparison.Ordinal);
        }
    }
}
