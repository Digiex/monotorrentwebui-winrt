using System;
using System.Configuration;
using System.ComponentModel;

namespace MonoTorrent.WebUI.Configuration
{
    /// <summary>
    /// Extends System.Configuration.ConfigurationSection with INotifyPropertyChanged.
    /// </summary>
    public abstract class ConfigurationSection : System.Configuration.ConfigurationSection, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
