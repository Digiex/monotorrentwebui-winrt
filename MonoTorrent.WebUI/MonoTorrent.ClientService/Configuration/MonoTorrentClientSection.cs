using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using System.IO;
using System.Net;
using MonoTorrent.Client.Encryption;
using System.ComponentModel;
using MonoTorrent.ClientService.Configuration.Converters;

namespace MonoTorrent.ClientService.Configuration
{
    /// <summary>
    /// COnfiguration for MonoTorrent client service.
    /// </summary>
    public class MonoTorrentClientSection : MonoTorrent.ClientService.Configuration.ConfigurationSection
    {
        /// <summary>
        /// Default name for this configuration section.
        /// </summary>
        public const string SectionName = "MonoTorrentClient";

        /// <summary>
        /// Initializes a new instance of MonoTorrent client configuration section.
        /// </summary>
        public MonoTorrentClientSection()
        {
            ConfigurationProperty savePath = new ConfigurationProperty("savePath",
                typeof(DirectoryInfo), ".",
                new DirectoryInfoConverter(),
                new DefaultValidator(),
                ConfigurationPropertyOptions.IsRequired,
                "Base directory where torrents' data is saved."
                );
            Properties.Add(savePath);

            ConfigurationProperty listenAddress = new ConfigurationProperty("listenAddress",
                typeof(IPAddress), IPAddress.Any,
                new IPAddressConverter(),
                new DefaultValidator(),
                ConfigurationPropertyOptions.None,
                "IP Address to listen on (optional)."
                );
            Properties.Add(listenAddress);

			ConfigurationProperty allowedEncryption = new ConfigurationProperty("allowedEncryption",
                typeof(EncryptionTypes), EncryptionTypes.All,
                new EncryptionTypesConverter(),
                new DefaultValidator(),
                ConfigurationPropertyOptions.None,
                "Allowed types of encryption for the BitTorrent protocol."
                );
            Properties.Add(allowedEncryption);
        }

        /// <summary>
        /// Directory where torrents' data is saved.
        /// </summary>
        public DirectoryInfo SavePath
        {
            get { return (DirectoryInfo)this["savePath"]; }
            set { this["savePath"] = value; }
        }

        /// <summary>
        /// Listen end point for the BitTorrent protocol.
        /// Wrapper for ListenAddress and ListenPort properties.
        /// </summary>
        public IPEndPoint ListenEndPoint
        {
            get { return new IPEndPoint(ListenAddress, ListenPort); }
            set 
            { 
                ListenAddress = value.Address;
                ListenPort = value.Port;
            }
        }

        /// <summary>
        /// Listen IP address for the BitTorrent protocol.
        /// </summary>
        public IPAddress ListenAddress
        {
            get { return (IPAddress)this["listenAddress"]; }
            set { this["listenAddress"] = value; OnPropertyChanged("ListenAddress"); }
        }

        /// <summary>
        /// Listen port for BitTorrent protocol.
        /// </summary>
        [ConfigurationProperty("listenPort", DefaultValue = 0, IsRequired = true)]
        [IntegerValidator(MinValue = IPEndPoint.MinPort, MaxValue = IPEndPoint.MaxPort)]
        public int ListenPort
        {
            get { return (int)this["listenPort"]; }
            set { this["listenPort"] = value; OnPropertyChanged("ListenAddress"); }
        }

        /// <summary>
        /// Global limit for the number of connections.
        /// </summary>
        [ConfigurationProperty("maxGlobalConn", DefaultValue = 0, IsRequired = true)]
        [IntegerValidator(MinValue = 0, MaxValue = int.MaxValue)]
        public int MaxGlobalConnections
        {
            get { return (int)this["maxGlobalConn"]; }
            set { this["maxGlobalConn"] = value; OnPropertyChanged("MaxGlobalConnections"); }
        }

        /// <summary>
        /// Global limit for the number of half-open connections.
        /// </summary>
        [ConfigurationProperty("maxHalfOpenConn", DefaultValue = 0, IsRequired = true)]
        [IntegerValidator(MinValue = 0, MaxValue = int.MaxValue)]
        public int MaxHalfOpenConnections
        {
            get { return (int)this["maxHalfOpenConn"]; }
            set { this["maxHalfOpenConn"] = value; OnPropertyChanged("MaxHalfOpenConnections"); }
        }

        /// <summary>
        /// Global limit for download rate. (bytes/sec)
        /// </summary>
        [ConfigurationProperty("maxDownloadRate", DefaultValue = 0, IsRequired = false)]
        [IntegerValidator(MinValue = 0, MaxValue = int.MaxValue)]
        public int MaxDownloadRate
        {
            get { return (int)this["maxDownloadRate"]; }
            set { this["maxDownloadRate"] = value; OnPropertyChanged("MaxDownloadRate"); }
        }

        /// <summary>
        /// Global limit for upload rate. (bytes/sec)
        /// </summary>
        [ConfigurationProperty("maxUploadRate", DefaultValue = 0, IsRequired = false)]
        [IntegerValidator(MinValue = 0, MaxValue = int.MaxValue)]
        public int MaxUploadRate
        {
            get { return (int)this["maxUploadRate"]; }
            set { this["maxUploadRate"] = value; OnPropertyChanged("MaxUploadRate"); }
        }

        /// <summary>
        /// Encryption which to utilize for the BitTorrent protocol.
        /// </summary>
        public EncryptionTypes AllowedEncryption
        {
            get { return (EncryptionTypes)this["allowedEncryption"]; }
            set { this["allowedEncryption"] = value; OnPropertyChanged("AllowedEncryption"); }
        }

        /// <summary>
        /// Default download rate limit for a torrent. (bytes/sec)
        /// </summary>
        [ConfigurationProperty("defaultDownloadRate", DefaultValue = 0, IsRequired = false)]
        [IntegerValidator(MinValue = 0, MaxValue = int.MaxValue)]
        public int DefaultDownloadRate
        {
            get { return (int)this["defaultDownloadRate"]; }
            set { this["defaultDownloadRate"] = value; OnPropertyChanged("DefaultDownloadRate"); }
        }

        /// <summary>
        /// Default upload rate limit for a torrent. (bytes/sec)
        /// </summary>
        [ConfigurationProperty("defaultUploadRate", DefaultValue = 0, IsRequired = false)]
        [IntegerValidator(MinValue = 0, MaxValue = int.MaxValue)]
        public int DefaultUploadRate
        {
            get { return (int)this["defaultUploadRate"]; }
            set { this["defaultUploadRate"] = value; OnPropertyChanged("DefaultUploadRate"); }
        }

        /// <summary>
        /// Default limit for the number of connections for a torrent.
        /// </summary>
        [ConfigurationProperty("defaultMaxConn", DefaultValue = 0, IsRequired = false)]
        [IntegerValidator(MinValue = 0, MaxValue = int.MaxValue)]
        public int DefaultMaxConnections
        {
            get { return (int)this["defaultMaxConn"]; }
            set { this["defaultMaxConn"] = value; OnPropertyChanged("DefaultMaxConnections"); }
        }

        /// <summary>
        /// Default number of upload slots for a torrent.
        /// </summary>
        [ConfigurationProperty("defaultUploadSlots", DefaultValue = 0, IsRequired = false)]
        [IntegerValidator(MinValue = 0, MaxValue = int.MaxValue)]
        public int DefaultUploadSlots
        {
            get { return (int)this["defaultUploadSlots"]; }
            set { this["defaultUploadSlots"] = value; OnPropertyChanged("DefaultUploadSlots"); }
        }
    }
}
