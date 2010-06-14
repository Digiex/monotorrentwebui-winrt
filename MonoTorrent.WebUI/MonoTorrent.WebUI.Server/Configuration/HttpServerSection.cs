using System;
using System.Configuration;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MonoTorrent.WebUI.Configuration.Converters;

namespace MonoTorrent.WebUI.Server.Configuration
{
    /// <summary>
    /// Parameters for a WebUI HTTP server.
    /// </summary>
    public class HttpServerSection : MonoTorrent.WebUI.Configuration.ConfigurationSection
    {
        /// <summary>
        /// Initializes a new instance of WebUI configuration section.
        /// </summary>
        public HttpServerSection()
        {
            // Following properties require custom converters which cannot be declared
            // using attributes hence they are declared like so:

            ConfigurationProperty rootPath = new ConfigurationProperty("rootPath", 
                typeof(DirectoryInfo), ".", 
                new DirectoryInfoConverter(),
                new DefaultValidator(),
                ConfigurationPropertyOptions.None,
                "Path to the website root directory."
                );
            Properties.Add(rootPath);
            
            ConfigurationProperty responseEncoding = new ConfigurationProperty("responseEncoding",
                typeof(Encoding), Encoding.Default,
                new EncodingConverter(),
                new DefaultValidator(),
                ConfigurationPropertyOptions.None,
                "Encoding used in HTTP responses."
                );
            Properties.Add(responseEncoding);

            ConfigurationProperty httpPrefix = new ConfigurationProperty("httpPrefix",
                typeof(string), null,
                null,
                HttpListenerValidator,
                ConfigurationPropertyOptions.IsRequired,
                "HttpListener prefix."
                );
            Properties.Add(httpPrefix);
        }

        /// <summary>
        /// Encoding to use in HTTP responses.
        /// </summary>
        public Encoding ResponseEncoding
        {
            get { return (Encoding)this["responseEncoding"]; }
            //set { this["responseEncoding"] = value; }
        }

        /// <summary>
        /// Directory containing static files.
        /// </summary>
        public DirectoryInfo WebSiteRoot
        {
            get { return (DirectoryInfo)this["rootPath"]; }
            //set { this["rootPath"] = value; OnPropertyChanged("WebSiteRoot"); }
        }

        /// <summary>
        /// Prefix ListeningAddress pattern to which the server listens. 
        /// See <see cref="System.Net.HttpListener"/> for format.
        /// </summary>
        public string HttpListenerPrefix
        {
            get { return (string)this["httpPrefix"]; }
            //set { this["httpPrefix"] = value; OnPropertyChanged("HttpListenerPrefix"); }
        }

        /// <summary>
        /// Returns the port that WebUI server is listening to.
        /// </summary>
        public int HttpListenerPort
        {
            get
            {
                Match match = Regex.Match(HttpListenerPrefix, @"^https?://.+:(?<port>[0-9]{1,5})/.*$");

                if (match.Success)
                    return int.Parse(match.Result("${port}"));
                else
                    return 80;
            }
        }

        /// <summary>
        /// Returns the port that WebUI server is listening to.
        /// </summary>
        public string HttpListenerPath
        {
            get
            {
                Match match = Regex.Match(HttpListenerPrefix, @"^https?://(.+?)/(?<base>.*)$");

                if (match.Success)
                    return match.Result("/${base}");
                else
                    return "/";
            }
        }


        /// <summary>
        /// Regex to match an IPv4 address
        /// </summary>
        protected const string IPRegex = @"(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])";

        /// <summary>
        /// Regex to match a DNS name
        /// </summary>
        protected const string DNSRegex = @"(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z]|[A-Za-z][A-Za-z0-9\-]*[A-Za-z0-9])";

        private static RegexStringValidator httpListenerValidator = new RegexStringValidator(
            @"^https?://(" + IPRegex + "|" + DNSRegex + "|[+]|[*])(:[0-9]{1,5})?(/(.*))?$"
            );
        protected virtual ConfigurationValidatorBase HttpListenerValidator
        {
            get { return httpListenerValidator; }
        }
    }
}
