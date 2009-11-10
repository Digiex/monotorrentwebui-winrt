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
    public partial class WebUISection
    {
        private const string IPRegex = @"(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])";
        private const string DNSRegex = @"(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z]|[A-Za-z][A-Za-z0-9\-]*[A-Za-z0-9])";

        /// <summary>
        /// Initializes a new instance of WebUI configuration section.
        /// </summary>
        private void InitializeHttpServerProperties()
        {
            // Following properties require custom converters which cannot be declared
            // using attributes hence they are declared like so:

            ConfigurationProperty pathWebUI = new ConfigurationProperty("path", 
                typeof(DirectoryInfo), Path.Combine(".", "WebUI"), 
                new DirectoryInfoConverter(),
                new DefaultValidator(),
                ConfigurationPropertyOptions.None,
                "Path to the WebUI directory."
                );
            Properties.Add(pathWebUI);
            
            ConfigurationProperty respEnc = new ConfigurationProperty("responseEncoding",
                typeof(Encoding), Encoding.Default,
                new EncodingConverter(),
                new DefaultValidator(),
                ConfigurationPropertyOptions.None,
                "Encoding used in HTTP responses."
                );
            Properties.Add(respEnc);

            ConfigurationProperty adminPass = new ConfigurationProperty("adminPass",
                typeof(string), null,
                ConfigurationPropertyOptions.None
                );
            Properties.Add(adminPass);
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
        /// Directory containing WebUI files.
        /// </summary>
        public DirectoryInfo DirWebUI
        {
            get { return (DirectoryInfo)this["path"]; }
            //set { this["path"] = value; OnPropertyChanged("DirWebUI"); }
        }

        /// <summary>
        /// Build number to report to WebUI.
        /// </summary>
        [ConfigurationProperty("build", DefaultValue = -1, IsRequired = false)]
        public int BuildNumber
        {
            get { return (int)this["build"]; }
            //set { this["build"] = value; OnPropertyChanged("BuildNumber"); }
        }

        /* Funny story: 
         * ConfigurationProperty.DefaultValue gets run through the property's
         * validators so it must be set to a valid value even though IsRequired = true
         * and the DefaultValue will never actually be put into action. The End. */

        /// <summary>
        /// Prefix URL pattern to which the server listens. 
        /// See <see cref="System.Net.HttpListener"/> for format.
        /// </summary>
        [ConfigurationProperty("httpPrefix", DefaultValue = "http://*/gui/", IsRequired = true)]
        [RegexStringValidator(@"^https?://(" + IPRegex + "|" + DNSRegex + "|[+]|[*])(:[0-9]{1,5})?/gui/$")]
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
    }
}
