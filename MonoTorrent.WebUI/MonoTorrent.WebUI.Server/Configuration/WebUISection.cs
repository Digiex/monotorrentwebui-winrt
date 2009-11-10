using System;

namespace MonoTorrent.WebUI.Server.Configuration
{
    public partial class WebUISection : MonoTorrent.WebUI.Configuration.ConfigurationSection
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
            InitializeHttpServerProperties();
            InitializeGeneralProperties();
        }
    }
}
