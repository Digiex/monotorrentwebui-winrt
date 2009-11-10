using System;
using System.Configuration;
using MonoTorrent.WebUI.Server.Utility;

namespace MonoTorrent.WebUI.Server.Configuration
{
    /// <summary>
    /// Configuration for a WebUI server.
    /// </summary>
    public partial class WebUISection
    {
        private void InitializeGeneralProperties()
        {
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
            this["adminPass"] = PasswordHelper.ToStorageFormat(clearPassword, HashAlgorithmName);
        }

        /// <summary>
        /// Determines if the <paramref name="candidate"/> matches the set password.
        /// </summary>
        /// <param name="candidate">Candidate password supplied by user.</param>
        /// <returns>True if the two match, false otherwise.</returns>
        public bool MatchAdminPassword(string candidate)
        {
            if (String.IsNullOrEmpty(candidate))
                return false;

            string adminPassHash = (string)this["adminPass"];

            if (String.IsNullOrEmpty(adminPassHash))
                return false;

            return PasswordHelper.IsPasswordMatch(candidate, adminPassHash);
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
        [CallbackValidator(CallbackMethodName = "HashAlgorithmNameValidator", Type = typeof(PasswordHelper))]
        public string HashAlgorithmName
        {
            get { return (string)this["hashAlgorithm"]; }
            set { this["hashAlgorithm"] = value;  OnPropertyChanged("GuestAccount"); }
        }
    }
}
