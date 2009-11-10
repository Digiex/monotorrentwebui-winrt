using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;

namespace MonoTorrent.WebUI.Server.Utility
{
    static class XmlFileLoader
    {
        public static XmlDocument LoadXmlDocument(string filePath)
        {
            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = new XmlCachingResolver();
            
            using (FileStream input = File.OpenRead(filePath))
            {
                doc.Load(input);
            }

            return doc;
        }
    }
}
