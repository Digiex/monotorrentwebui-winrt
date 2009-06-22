using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.ClientService
{
    internal class SetSettingsEventArgs : EventArgs
    {
        public SetSettingsEventArgs()
        {
        }

        public IEnumerable<KeyValuePair<string, string>> ModifiedSettings
        {
            get { yield break; }
        }
    }
}
