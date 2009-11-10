using System;
using System.Net;
using System.Net.Cache;
using System.Xml;
using System.IO;

namespace MonoTorrent.WebUI.Server.Utility
{
    /// <summary>
    /// XML Resolver which follows HttpRequestCachePolicy when getting entities.
    /// </summary>
    /// <seealso cref="http://msdn.microsoft.com/en-us/library/bb669135.aspx"/>
    class XmlCachingResolver : XmlUrlResolver
    {
        ICredentials credentials;

        /// <summary>
        /// Resolves resources from cache (if possible).
        /// </summary>
        public XmlCachingResolver() : base()
        {
        }

        /// <summary>
        /// Sets credentials used to authenticate Web requests.
        /// </summary>
        public override ICredentials Credentials
        {
            set
            {
                credentials = value;
                base.Credentials = value;
            }
        }

        public override object GetEntity(Uri absoluteUri, string role, Type ofObjectToReturn)
        {
            if (absoluteUri == null)
                throw new ArgumentNullException("absoluteUri");
            
            if (IsStreamType(ofObjectToReturn))
                return WebFetch(absoluteUri);
            else
                return base.GetEntity(absoluteUri, role, ofObjectToReturn);
        }

        private object WebFetch(Uri absoluteUri)
        {
            WebRequest webReq = WebRequest.Create(absoluteUri);
            webReq.CachePolicy = new RequestCachePolicy(RequestCacheLevel.Default);

            if (credentials != null)
                webReq.Credentials = credentials;

            WebResponse resp = webReq.GetResponse();

            return resp.GetResponseStream();
        }

        private static bool IsStreamType(Type ofObjectToReturn)
        {
            return (ofObjectToReturn == null || ofObjectToReturn == typeof(Stream));
        }
    }
}
