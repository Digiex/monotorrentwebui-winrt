using System;
using System.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace MonoTorrent.WebUI.Server.Configuration
{
    /// <summary>
    /// Configuration for a WebUI server.
    /// </summary>
    public partial class WebUISection
    {
        /// <summary>
        /// Hash algorithm instance. (Used for hashing passwords.)
        /// </summary>
        private HashAlgorithm hashAlg;

        private void InitializeGeneralProperties()
        {
            // Following properties require custom converters which cannot be declared
            // using attributes hence they are declared like so:

            ConfigurationProperty adminPass = new ConfigurationProperty("adminPass",
                typeof(string), null,
                ConfigurationPropertyOptions.None
                );
            Properties.Add(adminPass);
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

            throw new NotImplementedException();
            //this["adminPass"] = MonoTorrent.Common.Toolbox.ToHex(passwHash);
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

            throw new NotImplementedException();
            //string candHex = MonoTorrent.Common.Toolbox.ToHex(candHash);
            //string passHex = (string)this["adminPass"];

            //return String.Equals(candHex, passHex, StringComparison.InvariantCultureIgnoreCase);
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

        /// <summary>
        /// Hash algorithm used to hash passwords (for storage).
        /// </summary>
        [ConfigurationProperty("hashAlgorithm", DefaultValue = "SHA1", IsRequired = false)]
        public string HashAlgorithmName
        {
            get { return (string)this["hashAlgorithm"]; }
            set { this["hashAlgorithm"] = value; OnPropertyChanged("GuestAccount"); }
        }

        protected override void PostDeserialize()
        {
            hashAlg = HashAlgorithm.Create(HashAlgorithmName);

            base.PostDeserialize();
        }
    }
}
