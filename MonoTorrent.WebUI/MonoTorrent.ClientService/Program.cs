using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Configuration;
using System.IO;
using Newtonsoft.Json;

namespace MonoTorrent.ClientService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            //MonoTorrentClient client = new MonoTorrentClient();
            //ServiceBase[] servicesToRun = new ServiceBase[] 
            //{ 
            //    client,
            //    new ClientWebUI(filesWebUI, webUIport, client)
            //};
            //ServiceBase.Run(servicesToRun);

            Trace.Listeners.Add(new ConsoleTraceListener());

            string listenPrefix = ConfigurationManager.AppSettings["WebUI.HttpListenerPrefix"];
            DirectoryInfo filesWebUI = new DirectoryInfo(ConfigurationManager.AppSettings["WebUI.Files"]);

            MonoTorrentClient service = new MonoTorrentClient();
            ClientWebUI webUI = new ClientWebUI(filesWebUI, listenPrefix, service);

            Console.WriteLine("Starting MonoTorrent engine...");
            service.StartService();
            Console.WriteLine("MonoTorrent engine started.");

            Console.WriteLine("Starting WebUI...");
            webUI.StartService();
            Console.WriteLine("WebUI started.");

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
