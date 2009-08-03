using System;
using System.IO;
using System.Text;
using System.Configuration;
using System.Collections.Generic;
using MonoTorrent.ClientService.Configuration.Converters;
using System.Security.Cryptography;

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
        /// Instance of the machine's default hash algorithm. 
        /// (Used for hashing passwords.)
        /// </summary>
        private static readonly HashAlgorithm hashAlg = HashAlgorithm.Create();

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

        /// <summary>
        /// Admin username.
        /// </summary>
        [ConfigurationProperty("adminUser", DefaultValue = "admin", IsRequired = false)]
        public string AdminUsername
        {
            get { return (string)this["adminUser"]; }
            set { this["adminUser"] = value; OnPropertyChanged("AdminUsername"); }
        }


        /// <summary>
        /// Stores the hashed admin user's password.
        /// </summary>
        /// <param name="clearPassword"></param>
        public void SetAdminPassword(string clearPassword)
        {
            byte[] passwBytes = Encoding.Default.GetBytes(clearPassword);
            byte[] passwHash = hashAlg.ComputeHash(passwBytes);

            this["adminPass"] = MonoTorrent.Common.Toolbox.ToHex(passwHash);
        }

        /// <summary>
        /// Determines if the <paramref name="candidate"/> matches the original password.
        /// </summary>
        /// <param name="candidate">Candidate password supplied by user.</param>
        /// <returns>True if the two match, false otherwise.</returns>
        public bool MatchAdminPassword(string candidate)
        {
            byte[] candBytes = Encoding.Default.GetBytes(candidate);
            byte[] candHash = hashAlg.ComputeHash(candBytes);

            string candHex = MonoTorrent.Common.Toolbox.ToHex(candHash);
            string passHex = (string)this["adminPass"];

            return String.Equals(candHex, passHex, StringComparison.InvariantCultureIgnoreCase);
        }

        /// <summary>
        /// True if guest account access should be enabled.
        /// </summary>
        [ConfigurationProperty("enableGuest", DefaultValue = false, IsRequired = false)]
        public bool EnableGuest
        {
            get { return (bool)this["enableGuest"]; }
            set { this["enableGuest"] = value; OnPropertyChanged("EnableGuest"); }
        }

        /// <summary>
        /// Guest account name.
        /// </summary>
        [ConfigurationProperty("guestAccount", DefaultValue = "guest", IsRequired = false)]
        public string GuestAccount
        {
            get { return (string)this["guestAccount"]; }
            set { this["guestAccount"] = value; OnPropertyChanged("GuestAccount"); }
        }
    }
}
