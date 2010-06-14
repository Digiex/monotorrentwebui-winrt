using System;
using System.Diagnostics;
using MonoTorrent.WebUI.Server;
using MonoTorrent.WebUI.Server.Configuration;
using System.ServiceProcess;

namespace MonoTorrent.ClientService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            MonoTorrentClient monoTorrent = new MonoTorrentClient();
            ServiceWebUI webUI = new ServiceWebUI(monoTorrent);

            //ServiceBase[] servicesToRun = new ServiceBase[] { monoTorrent, webUI };
            //ServiceBase.Run(servicesToRun);

            RunConsole(monoTorrent, webUI);
        }

        [Conditional("DEBUG")]
        static void RunConsole(MonoTorrentClient monoTorrent, ServiceWebUI webUI)
		{
            InitTrace();

            Trace.WriteLine("Starting MonoTorrent engine...");
            monoTorrent.DebugStart();
            Trace.WriteLine("MonoTorrent engine started.");

            Trace.WriteLine("Starting WebUI...");
            webUI.DebugStart();
            Trace.WriteLine("WebUI running on " + webUI.ListeningAddress + ".");
            
            Console.WriteLine("(Press ESC to halt)");
            while (Console.ReadKey().Key != ConsoleKey.Escape) { }

            Trace.WriteLine("Stopping WebUI...");
            webUI.DebugStop();
            Trace.WriteLine("WebUI stopped.");

            Trace.WriteLine("Stopping MonoTorrent engine...");
            monoTorrent.DebugStop();
            Trace.WriteLine("MonoTorrent engine stopped.");

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
		}

        [Conditional("TRACE")]
        static void InitTrace()
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
        }
    }
}
