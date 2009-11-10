using System;
using System.Collections.Generic;

namespace MonoTorrent.WebUI.Server.Utility
{
    /// <summary>
    /// Provides a mapping between file extensions and MIME types.
    /// </summary>
    internal static class MimeTypes
    {
        private static Dictionary<string, string> extensionToMime = new Dictionary<string,string>();

        static MimeTypes()
        {
            RegisterExtension(".ai", "application/postscript");
            RegisterExtension(".aif", "audio/aiff");
            RegisterExtension(".aifc", "audio/aiff");
            RegisterExtension(".aiff", "audio/aiff");
            RegisterExtension(".asp", "text/asp");
            RegisterExtension(".asx", "video/x-ms-asf");
            RegisterExtension(".au", "audio/basic");   
            RegisterExtension(".avi", "video/avi");
            RegisterExtension(".bin", "application/octet-stream");
            RegisterExtension(".bmp", "image/bmp");       
            RegisterExtension(".bz", "application/x-bzip");
            RegisterExtension(".bz2", "application/x-bzip2");
            RegisterExtension(".c", "text/plain");
            RegisterExtension(".c++", "text/plain");
            RegisterExtension(".cc", "text/plain");
            RegisterExtension(".class", "application/java");
            RegisterExtension(".com", "application/octet-stream");
            RegisterExtension(".conf", "text/plain");
            RegisterExtension(".cpp", "text/x-c");
            RegisterExtension(".doc", "application/msword");
            RegisterExtension(".dot", "application/msword");
            RegisterExtension(".eps", "application/postscript");
            RegisterExtension(".gif", "image/gif");
            RegisterExtension(".gz", "application/x-gzip");
            RegisterExtension(".gzip", "application/x-gzip");
            RegisterExtension(".h", "text/plain");
            RegisterExtension(".inf", "application/inf");
            RegisterExtension(".jav", "text/x-java-source");
            RegisterExtension(".java", "text/x-java-source");
            RegisterExtension(".jcm", "application/x-java-commerce");
            RegisterExtension(".latex", "application/x-latex");
            RegisterExtension(".m1v", "video/mpeg");
            RegisterExtension(".m2a", "audio/mpeg");
            RegisterExtension(".m2v", "video/mpeg");
            RegisterExtension(".m3u", "audio/x-mpequrl");
            RegisterExtension(".mht", "message/rfc822");
            RegisterExtension(".mhtml", "message/rfc822");
            RegisterExtension(".mid", "audio/midi");
            RegisterExtension(".midi", "audio/midi");
            RegisterExtension(".moov", "video/quicktime");
            RegisterExtension(".mov", "video/quicktime");
            RegisterExtension(".mp2", "audio/mpeg");
            RegisterExtension(".mp3", "audio/mpeg3");
            RegisterExtension(".mpa", "video/mpeg");
            RegisterExtension(".mpe", "video/mpeg");
            RegisterExtension(".mpeg", "video/mpeg");
            RegisterExtension(".mpg", "video/mpeg");
            RegisterExtension(".mpga", "audio/mpeg");
            RegisterExtension(".mpp", "application/vnd.ms-project");
            RegisterExtension(".ppa", "application/vnd.ms-powerpoint");
            RegisterExtension(".pps", "application/vnd.ms-powerpoint");
            RegisterExtension(".ppt", "application/vnd.ms-powerpoint");
            RegisterExtension(".ps", "application/postscript");
            RegisterExtension(".psd", "application/octet-stream");
            RegisterExtension(".rt", "text/richtext");
            RegisterExtension(".swf", "application/x-shockwave-flash");
            RegisterExtension(".wav", "audio/wav");
            RegisterExtension(".css", "text/css");
            RegisterExtension(".jpeg", "image/jpeg");
            RegisterExtension(".jpg", "image/jpeg");
            RegisterExtension(".png", "image/png");
            RegisterExtension(".htm", "text/html");
            RegisterExtension(".html", "text/html");
            RegisterExtension(".ico", "image/x-icon");
            RegisterExtension(".js", "text/javascript");
            RegisterExtension(".rtf", "text/richtext");
            RegisterExtension(".text", "text/plain");
            RegisterExtension(".tiff", "image/tiff");
            RegisterExtension(".txt", "text/plain");
            RegisterExtension(".xml", "text/xml");
            RegisterExtension(".zip", "application/zip");
            RegisterExtension(".exe", "application/octet-stream");
            RegisterExtension(".xls", "application/vnd.ms-excel");
            RegisterExtension(".vsd", "application/x-visio");
            RegisterExtension(".tgz", "application/gnutar");
        }

        private static void RegisterExtension(string ext, string mime)
        {
            extensionToMime.Add(ext, mime);
        }

        /// <summary>
        /// Maps a file extension to a MIME string or "application/unknown" 
        /// if the extension is unknown.
        /// </summary>
        /// <param name="extension">File extension with the leading '.' included.</param>
        /// <returns>MIME string which corresponds to the extension.</returns>
        public static string ExtensionLookup(string extension)
        {
            string mimeType;
            if (extensionToMime.TryGetValue(extension, out mimeType))
                return mimeType;
            else
                return "application/unknown";
        }
    }
}
