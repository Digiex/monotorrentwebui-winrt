using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using System.IO;
using System.Net;
using MonoTorrent.Client.Encryption;
using System.ComponentModel;

namespace MonoTorrent.ClientService.Configuration
{
    public class MonoTorrentClientSection : ConfigurationSection
    {
        public MonoTorrentClientSection()
        {
            ConfigurationProperty savePath = new ConfigurationProperty("savePath",
                typeof(DirectoryInfo), ".",
                new DirectoryInfoConverter(),
                new DefaultValidator(),
                ConfigurationPropertyOptions.IsRequired,
                "Path where torrents are downloaded."
                );
            Properties.Add(savePath);

            ConfigurationProperty listenAddress = new ConfigurationProperty("listenAddress",
                typeof(IPAddress), IPAddress.Any,
                new TypeConverter(),
                new DefaultValidator(),
                ConfigurationPropertyOptions.None,
                "IP Address to listen (optional)."
                );
            Properties.Add(listenAddress);
        }

        public DirectoryInfo SavePath
        {
            get { return (DirectoryInfo)this["savePath"]; }
            set { this["savePath"] = value; }
        }

        public IPEndPoint ListenEndPoint
        {
            get { return new IPEndPoint(ListenAddress, ListenPort); }
        }

        public IPAddress ListenAddress
        {
            get { return (IPAddress)this["listenAddress"]; }
            set { this["listenAddress"] = value; }
        }

        [ConfigurationProperty("listenPort", DefaultValue = 0, IsRequired = true)]
        [IntegerValidator(MinValue = IPEndPoint.MinPort, MaxValue = IPEndPoint.MaxPort)]
        public int ListenPort
        {
            get { return (int)this["listenPort"]; }
            set { this["listenPort"] = value; }
        }

        [ConfigurationProperty("maxGlobalConn", DefaultValue = 0, IsRequired = true)]
        [IntegerValidator(MinValue = 0, MaxValue = int.MaxValue)]
        public int MaxGlobalConnections
        {
            get { return (int)this["maxGlobalConn"]; }
            set { this["maxGlobalConn"] = value; }
        }

        [ConfigurationProperty("maxHalfOpenConn", DefaultValue = 0, IsRequired = true)]
        [IntegerValidator(MinValue = 0, MaxValue = int.MaxValue)]
        public int MaxHalfOpenConnections
        {
            get { return (int)this["maxHalfOpenConn"]; }
            set { this["maxHalfOpenConn"] = value; }
        }

        [ConfigurationProperty("maxDownloadRate", DefaultValue = 0, IsRequired = false)]
        [IntegerValidator(MinValue = 0, MaxValue = int.MaxValue)]
        public int MaxDownloadRate
        {
            get { return (int)this["maxDownloadRate"]; }
            set { this["maxDownloadRate"] = value; }
        }

        [ConfigurationProperty("maxUploadRate", DefaultValue = 0, IsRequired = false)]
        [IntegerValidator(MinValue = 0, MaxValue = int.MaxValue)]
        public int MaxUploadRate
        {
            get { return (int)this["maxUploadRate"]; }
            set { this["maxUploadRate"] = value; }
        }

        [ConfigurationProperty("encryptionFlags", DefaultValue = 0, IsRequired = false)]
        public EncryptionTypes EncryptionFlags
        {
            get { return (EncryptionTypes)this["encryptionFlags"]; }
            set { this["encryptionFlags"] = value; }
        }

        [ConfigurationProperty("defaultDownloadRate", DefaultValue = 0, IsRequired = false)]
        [IntegerValidator(MinValue = 0, MaxValue = int.MaxValue)]
        public int DefaultDownloadRate
        {
            get { return (int)this["defaultDownloadRate"]; }
            set { this["defaultDownloadRate"] = value; }
        }

        [ConfigurationProperty("defaultUploadRate", DefaultValue = 0, IsRequired = false)]
        [IntegerValidator(MinValue = 0, MaxValue = int.MaxValue)]
        public int DefaultUploadRate
        {
            get { return (int)this["defaultUploadRate"]; }
            set { this["defaultUploadRate"] = value; }
        }

        [ConfigurationProperty("defaultMaxConn", DefaultValue = 0, IsRequired = false)]
        [IntegerValidator(MinValue = 0, MaxValue = int.MaxValue)]
        public int DefaultMaxConnections
        {
            get { return (int)this["defaultMaxConn"]; }
            set { this["defaultMaxConn"] = value; }
        }

        [ConfigurationProperty("defaultUploadSlots", DefaultValue = 0, IsRequired = false)]
        [IntegerValidator(MinValue = 0, MaxValue = int.MaxValue)]
        public int DefaultUploadSlots
        {
            get { return (int)this["defaultUploadSlots"]; }
            set { this["defaultUploadSlots"] = value; }
        }
    }
}
