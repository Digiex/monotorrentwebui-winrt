using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.WebUI.Server
{
    /// <summary>
    /// Sets the value of the setting.
    /// </summary>
    /// <param name="value">String representation of the value.</param>
    internal delegate void SettingSetter(string value);

    /// <summary>
    /// Retrieves the value of the setting.
    /// </summary>
    /// <returns>Current value of the setting.</returns>
    internal delegate object SettingGetter();

    /// <summary>
    /// Ecapsulates the logic needed to 
    /// </summary>
    internal class Setting
    {
        private Setting() { }

        /// <summary>
        /// Creates an encapsulated setting logic and parameters.
        /// </summary>
        /// <param name="name">WebUI name of the setting.</param>
        /// <param name="type">Type of value represented.</param>
        /// <param name="get">Delegate to handle retrieval of the setting's value.</param>
        /// <param name="set">Delegate to handle setting the setting's value.</param>
        public Setting(string name, WebSettingType type, SettingGetter get, SettingSetter set)
        {
            this.Name = name;
            this.Type = type;
            this.Get = get;
            this.Set = set;
        }

        public readonly string Name;
        public readonly WebSettingType Type;
        public readonly SettingGetter Get;
        public readonly SettingSetter Set;

        /// <summary>
        /// Executes the Get delegate and converts the value to a string.
        /// </summary>
        /// <returns>WebUI representation of the setting value.</returns>
        public string GetStringValue()
        {
            object value = Get();

            if (value == null)
                return null;

            if (Type == WebSettingType.Boolean)
                return value.ToString().ToLowerInvariant();
            else
                return value.ToString();
        }

        /// <summary>
        /// WebUI setting type representation.
        /// </summary>
        public enum WebSettingType : int
        {
            Integer = 0,
            Boolean = 1,
            String = 2
        }

        public override string ToString()
        {
            return String.Format("{0} = \"{1}\"", Name, GetStringValue() ?? "");
        }
    }
}
