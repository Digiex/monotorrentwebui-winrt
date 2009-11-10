using System;
using System.Diagnostics;
using MonoTorrent.WebUI.Server;
using MonoTorrent.WebUI.Server.Configuration;

namespace MonoTorrent.ClientService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            //MonoTorrentClient service = new MonoTorrentClient();
            //ClientWebUI webUI = new ClientWebUI(service);
            //ServiceBase[] servicesToRun = new ServiceBase[] { service, webUI };
            //ServiceBase.Run(servicesToRun);

			RunConsole();
        }

        [Conditional("DEBUG")]
		static void RunConsole()
		{
            InitTrace();

            MonoTorrentClient monoTorrent = new MonoTorrentClient();
            ServiceWebUI webUI = new ServiceWebUI(monoTorrent);

            Trace.WriteLine("Starting MonoTorrent engine...");
            monoTorrent.DebugStart();
            Trace.WriteLine("MonoTorrent engine started.");

            Trace.WriteLine("Starting WebUI...");
            webUI.DebugStart();
            Trace.WriteLine("WebUI running.");
            
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
