using System;
using System.Configuration;

namespace MonoTorrent.ClientService
{
    /// <summary>
    /// Implements loading and saving of configuration.
    /// </summary>
    public abstract class ConfiguredServiceBase<TConfigSection> : System.ServiceProcess.ServiceBase
        where TConfigSection : System.Configuration.ConfigurationSection
    {
        /// <summary>
        /// Should never call this constructor.
        /// </summary>
        private ConfiguredServiceBase() : base() { }

        /// <summary>
        /// Initializes configuration section information.
        /// </summary>
        /// <param name="configSectionName">
        /// Configuration section name that will be loaded into Config property.
        /// Type must match <typeparamref name="TConfigSection"/>.
        /// </param>
        protected ConfiguredServiceBase(string configSectionName)
            : base()
        {
            this.configSectionName = configSectionName;
        }

        /// <summary>
        /// Name of the configuration section to pull.
        /// </summary>
        private readonly string configSectionName;

        /// <summary>
        /// Configuration loaded form a file or the default default "app.config".
        /// </summary>
        protected System.Configuration.Configuration Configuration
        {
            get;
            private set;
        }

        /// <summary>
        /// Service configuration section.
        /// </summary>
        internal TConfigSection Config
        {
            get;
            private set;
        }

        /// <summary>
        /// Loads the WebUI configuration section.
        /// </summary>
        private void LoadConfigFile(string[] args)
        {
            if (args.Length > 0)
                Configuration = ConfigurationManager.OpenExeConfiguration(args[0]);
            else
                Configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            Config = (TConfigSection)Configuration.GetSection(configSectionName);

            OnConfigurationLoaded();
        }

        /// <summary>
        /// Fires when the configuration has been loaded or the section re-read.
        /// </summary>
        public event EventHandler ConfigurationLoaded;

        /// <summary>
        /// Invokes the ConfigurationLoaded event.
        /// </summary>
        private void OnConfigurationLoaded()
        {
            if (ConfigurationLoaded != null)
                ConfigurationLoaded(this, EventArgs.Empty);
        }

        /// <summary>
        /// Applies the currently loaded configuration section.
        /// </summary>
        protected abstract void ApplyConfiguration();

        /// <summary>
        /// Propagates values into the configuration section object.
        /// </summary>
        protected abstract void CollectConfiguration();

        #region Service Events
        /// <summary>
        /// Loads the configuration file and calls base.OnStart(args).
        /// </summary>
        protected override void OnStart(string[] args)
        {
            LoadConfigFile(args);

            ApplyConfiguration();

            base.OnStart(args);
        }

        /// <summary>
        /// Saves configuration and calls base.OnShutdown().
        /// </summary>
        protected override void OnShutdown()
        {
            CollectConfiguration();

            Configuration.Save();

            base.OnShutdown();
        } 
        #endregion
    }
}
