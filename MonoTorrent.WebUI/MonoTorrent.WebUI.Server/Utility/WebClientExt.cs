using System;
using System.Net;

namespace MonoTorrent.WebUI.Server.Utility
{
    /// <summary>
    /// Adds MaxDownloadSize and CookieContainer support to System.Net.WebClient.
    /// </summary>
    class WebClientExt : WebClient
    {
        /// <summary>
        /// Maximum download size.
        /// </summary>
        public int MaxDownloadSize { get; set; }

        /// <summary>
        /// Cookies to use when making requests.
        /// </summary>
        public CookieContainer CookieContainer { get; set; }

        public WebClientExt() : base()
        {
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            return GetWebResponseExt(request);
        }

        protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result)
        {
            return GetWebResponseExt(request);
        }

        private WebResponse GetWebResponseExt(WebRequest request)
        {
            if (CookieContainer != null)
            {
                string cookieHeader = CookieContainer.GetCookieHeader(request.RequestUri);
                request.Headers[HttpRequestHeader.Cookie] = cookieHeader;
            }

            WebResponse response = base.GetWebResponse(request);

            CheckContentLength(response.ContentLength);

            return response;
        }

        protected override void OnDownloadProgressChanged(DownloadProgressChangedEventArgs e)
        {
            CheckContentLength(e.BytesReceived);

            base.OnDownloadProgressChanged(e);
        }

        private void CheckContentLength(long byteCount)
        {
            if (byteCount > MaxDownloadSize)
                throw new WebException(
                    "Downloaded content exceeds the specified size limit. (See MaxDownloadSize)"
                    );
        }
    }
}
