using System;
using System.Configuration;
using System.ComponentModel;

namespace MonoTorrent.ClientService.Configuration
{
    /// <summary>
    /// Extends System.Configuration.ConfigurationSection with INotifyPropertyChanged interface.
    /// </summary>
    public abstract class ConfigurationSection : System.Configuration.ConfigurationSection, INotifyPropertyChanged
    {
        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        protected void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
