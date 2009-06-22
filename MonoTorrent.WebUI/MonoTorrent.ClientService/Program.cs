using System;
using System.Diagnostics;
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
            //MonoTorrentClient service = new MonoTorrentClient();
            //ClientWebUI webUI = new ClientWebUI(service);
            //ServiceBase[] servicesToRun = new ServiceBase[] { service, webUI };
            //ServiceBase.Run(servicesToRun);

			RunConsole();
        }

        [Conditional("DEBUG")]
		static void RunConsole()
		{
			Trace.Listeners.Add(new ConsoleTraceListener());

            MonoTorrentClient torrent = new MonoTorrentClient();
            ClientWebUI webUI = new ClientWebUI(torrent);

            Trace.WriteLine("Starting MonoTorrent engine...");
            torrent.StartService();
            Trace.WriteLine("MonoTorrent engine started.");

            Trace.WriteLine("Starting WebUI...");
            webUI.SynthStart();
            Trace.WriteLine("WebUI running.");

            while (Console.ReadKey().Key != ConsoleKey.Escape) { }

            Trace.WriteLine("Stopping WebUI...");
            webUI.SynthStop();
            Trace.WriteLine("WebUI stopped.");

            Trace.WriteLine("Stopping MonoTorrent engine...");
            torrent.StopService();
            Trace.WriteLine("MonoTorrent engine stopped.");

            Console.ReadKey();
		}
    }
}
