using System;
using System.Collections.Generic;
using System.Text;

namespace MonoTorrent.ClientService
{
    public static class Settings
    {
        public static int DefaultMaxDownloadSpeed
        {
            get { return 100000; }
        }

        public static int DefaultMaxUploadSpeed
        {
            get { return 10000; }
        }

        public static int DefaultMaxConnections
        {
            get { return 100; }
        }

        public static int DefaultUploadSlots
        {
            get { return 4; }
        }
    }
}
