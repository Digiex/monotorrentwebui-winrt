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

			RunDebug();
        }
		
		static void RunDebug()
		{
			Trace.Listeners.Add(new ConsoleTraceListener());

            MonoTorrentClient service = new MonoTorrentClient();
            ClientWebUI webUI = new ClientWebUI(service);

            Console.WriteLine("Starting MonoTorrent engine...");
            service.StartService();
            Console.WriteLine("MonoTorrent engine started.");

            Console.WriteLine("Starting WebUI...");
            webUI.StartService();
            Console.WriteLine("WebUI running.");

            while (Console.ReadKey().Key != ConsoleKey.Escape) { }

            Console.WriteLine("Stopping WebUI...");
            webUI.StopService();
            Console.WriteLine("WebUI stopped.");

            Console.WriteLine("Stopping MonoTorrent engine...");
            service.StopService();
            Console.WriteLine("MonoTorrent engine stopped.");

            Console.ReadKey();
		}
    }
}
