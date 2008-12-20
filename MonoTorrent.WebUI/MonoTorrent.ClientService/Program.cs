using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Configuration;
using System.IO;

namespace MonoTorrent.ClientService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            //ServiceBase[] ServicesToRun;
            //ServicesToRun = new ServiceBase[] 
            //{ 
            //    new MonoTorrentClientService(),
            //    new ClientWebUI(filesWebUI, webUIport, service)
            //};
            //ServiceBase.Run(ServicesToRun);

            Trace.Listeners.Add(new ConsoleTraceListener());

            int webUIport = int.Parse(ConfigurationManager.AppSettings["WebUI.Port"]);
            DirectoryInfo filesWebUI = new DirectoryInfo(ConfigurationManager.AppSettings["WebUI.Files"]);
            if (!filesWebUI.Exists)
                throw new Exception("WebUI directory does not exist.");

            MonoTorrentClient<string> service = new MonoTorrentClient<string>();
            ClientWebUI webUI = new ClientWebUI(filesWebUI, webUIport, service);

            Console.WriteLine("Starting Service...");
            service.StartService();
            Console.WriteLine("Service Started.");

            Console.WriteLine("Starting WebUI...");
            webUI.StartService();
            Console.WriteLine("WebUI Started.");

            while (Console.ReadKey().Key != ConsoleKey.Escape) { }

            Console.WriteLine("Stopping WebUI...");
            webUI.StopService();
            Console.WriteLine("WebUI Stopped.");

            Console.WriteLine("Stopping Service...");
            service.StopService();
        }
    }
}
