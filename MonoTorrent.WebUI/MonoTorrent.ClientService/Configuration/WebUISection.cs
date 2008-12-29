using System;
using System.Collections.Generic;
using System.Text;
using System.Configuration;
using System.IO;
using System.ComponentModel;

namespace MonoTorrent.ClientService.Configuration
{
    public class WebUISection : ConfigurationSection
    {
        public WebUISection()
        {
            ConfigurationProperty pathWebUI = new ConfigurationProperty("path", 
                typeof(DirectoryInfo), ".", 
                new DirectoryInfoConverter(),
                new DefaultValidator(),
                ConfigurationPropertyOptions.None,
                "Path to the WebUI directory."
                );
            Properties.Add(pathWebUI);

            ConfigurationProperty respEnc = new ConfigurationProperty("responseEncoding",
                typeof(Encoding), Encoding.Default,
                new EncodingConverter(),
                new DefaultValidator(),
                ConfigurationPropertyOptions.None,
                "Encoding used for web responses."
                );
            Properties.Add(respEnc);
        }

        public override bool IsReadOnly()
        {
            return true;
        }

        //[ConfigurationProperty("responseEncoding", IsRequired = true)]
        public Encoding ResponseEncoding
        {
            get { return (Encoding)this["responseEncoding"]; }
            set { this["responseEncoding"] = value; }
        }

        [ConfigurationProperty("build", IsRequired = true)]
        public int BuildNumber
        {
            get { return (int)this["build"]; }
            set { this["build"] = value; }
        }

        //[ConfigurationProperty("path", IsRequired = true)]
        public DirectoryInfo DirWebUI
        {
            get { return (DirectoryInfo)this["path"]; }
            set { this["path"] = value; }
        }

        // Default property is run through the validator, so it can't be left empty
        [ConfigurationProperty("httpPrefix", DefaultValue="http://*/gui/", IsRequired = true)]
        [RegexStringValidator(@"https?://(([a-zA-Z0-9]+(.[a-zA-Z0-9]+)*[a-zA-Z0-9])|[+]|[*])(:[0-9]{1,5})?/gui/")]
        public string HttpListenerPrefix
        {
            get { return (string)this["httpPrefix"]; }
            set { this["httpPrefix"] = value; }
        }

        //[CallbackValidator(Type = typeof(WebUISection), CallbackMethodName = "ValidateProperty")]
        public static void ValidateProperty(object value)
        {
            System.Diagnostics.Debug.WriteLine("Validating: " + value);
        }
    }
}
