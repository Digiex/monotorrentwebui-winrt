using System;
using System.Configuration;

namespace MonoTorrent.WebUI.Configuration
{
    /// <summary>
    /// Implements loading and saving of configuration.
    /// </summary>
    public class ConfiguredServiceBase<TSection> : System.ServiceProcess.ServiceBase
        where TSection : System.Configuration.ConfigurationSection
    {
        /// <summary>
        /// Hide this constructor.
        /// </summary>
        private ConfiguredServiceBase() : base() { }

        /// <summary>
        /// Initializes configuration information.
        /// </summary>
        /// <param name="configSectionName">
        /// Configuration section name that will be loaded into Config property.
        /// Type must match <typeparamref name="TConfigSection"/>.
        /// </param>
        protected ConfiguredServiceBase(string sectionName)
            : base()
        {
            this.SectionName = sectionName;
        }

        /// <summary>
        /// Name of the configuration section to pull.
        /// </summary>
        protected readonly string SectionName;

        /// <summary>
        /// Configuration loaded form a file or the default default "app.config".
        /// </summary>
        protected System.Configuration.Configuration Configuration
        {
            get;
            private set;
        }

        protected void SaveConfiguration()
        {
            Configuration.Save(ConfigurationSaveMode.Minimal);
        }

        protected TSection Config
        {
            get { return (TSection)Configuration.GetSection(SectionName); }
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

            OnConfigurationLoaded();
        }

        /// <summary>
        /// Fires when the configuration has been loaded or the section re-read.
        /// </summary>
        //public event EventHandler ConfigurationLoaded;

        /// <summary>
        /// Invokes the ConfigurationLoaded event.
        /// </summary>
        private void OnConfigurationLoaded()
        {
            //if (ConfigurationLoaded != null)
            //    ConfigurationLoaded(this, EventArgs.Empty);
        }

        /// <summary>
        /// Applies the currently loaded configuration section.
        /// </summary>
        protected virtual void OnApplyConfiguration()
        {
        }

        /// <summary>
        /// Propagates values into the configuration section object.
        /// </summary>
        protected virtual void OnCollectConfiguration()
        {
        }

        #region Service Events
        /// <summary>
        /// Loads the configuration file and calls base.OnStart(args).
        /// </summary>
        protected override void OnStart(string[] args)
        {
            base.OnStart(args);

            LoadConfigFile(args);

            OnApplyConfiguration();
        }

        /// <summary>
        /// Saves configuration and calls base.OnShutdown().
        /// </summary>
        protected override void OnShutdown()
        {
            OnCollectConfiguration();

            Configuration.Save();

            base.OnShutdown();
        } 
        #endregion
    }
}
