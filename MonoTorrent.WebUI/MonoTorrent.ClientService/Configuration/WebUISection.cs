using System;
using System.IO;
using System.Text;
using System.Configuration;
using System.Collections.Generic;
using MonoTorrent.ClientService.Configuration.Converters;

namespace MonoTorrent.ClientService.Configuration
{
    /// <summary>
    /// Configuration for the WebUI service.
    /// </summary>
    public class WebUISection : MonoTorrent.ClientService.Configuration.ConfigurationSection
    {
        /// <summary>
        /// Default name for this configuration section.
        /// </summary>
        public const string SectionName = "WebUI";

        /// <summary>
        /// Initializes a new instance of WebUI configuration section.
        /// </summary>
        public WebUISection()
        {
            // Following properties require custom converters which cannot be declared
            // using attributes hence they are declared like so:

            ConfigurationProperty pathWebUI = new ConfigurationProperty("path", 
                typeof(DirectoryInfo), ".", 
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
                "Encoding used for web responses."
                );
            Properties.Add(respEnc);
        }

        /// <summary>
        /// Encoding to use in HTTP responses.
        /// </summary>
        public Encoding ResponseEncoding
        {
            get { return (Encoding)this["responseEncoding"]; }
            set { this["responseEncoding"] = value; }
        }

        /// <summary>
        /// Build number to report to WebUI.
        /// </summary>
        [ConfigurationProperty("build", DefaultValue = -1, IsRequired = false)]
        public int BuildNumber
        {
            get { return (int)this["build"]; }
            set { this["build"] = value; OnPropertyChanged("BuildNumber"); }
        }

        /// <summary>
        /// Directory containing WebUI files.
        /// </summary>
        public DirectoryInfo DirWebUI
        {
            get { return (DirectoryInfo)this["path"]; }
            set { this["path"] = value; OnPropertyChanged("DirWebUI"); }
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
        [RegexStringValidator(@"https?://(([a-zA-Z0-9]+(.[a-zA-Z0-9]+)*[a-zA-Z0-9])|[+]|[*])(:[0-9]{1,5})?/gui/")]
        public string HttpListenerPrefix
        {
            get { return (string)this["httpPrefix"]; }
            set { this["httpPrefix"] = value; OnPropertyChanged("HttpListenerPrefix"); }
        }
    }
}
