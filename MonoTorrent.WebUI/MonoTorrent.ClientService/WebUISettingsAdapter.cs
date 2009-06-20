// SettingsAdapter.cs created with MonoDevelop
// User: serguei at 16:45 29.03.2009
//
// To change standard headers go to Edit->Preferences->Coding->Standard Headers
//

using System;
using System.Collections.Generic;
using MonoTorrent.Client;

namespace MonoTorrent.ClientService
{
	/// <summary>
	/// Adapter between WebUI and MonoTorrent settings. 
	/// </summary>
	public class WebUISettingsAdapter : IEnumerable<KeyValuePair<string, string>>
	{
		private EngineSettings client;
		private Dictionary<string, string> settingValues;
		
		public WebUISettingsAdapter(EngineSettings client)
		{
			this.client = client;
			settingValues = new Dictionary<string,string>();
			
		}
		
		public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
		{
			return settingValues.GetEnumerator();
		}

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
