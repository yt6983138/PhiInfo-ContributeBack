using System;
using System.CommandLine;
using System.IO;
using System.Threading;
using AlphaOmega.Debug;
using AlphaOmega.Debug.Manifest;

namespace PhiInfo.CLI
{
    public class CLIHttpServer(string apkPath, Stream cldbStream) : HttpServer(apkPath, cldbStream)
    {
        protected override string LoadVersionCode()
        {
            using AxmlFile axml = new(new StreamLoader(_zip.OpenFileStreamByName("AndroidManifest.xml")));
            ArscFile arsc = new(_zip.OpenFileStreamByName("resources.arsc"));
            var Manifest = AndroidManifest.Load(axml, arsc);
            return Manifest.VersionCode;
        }
    }

    class Program
    {
        static int Main(string[] args)
        {
            Option<FileInfo> apkOption = new("--apk")
            {
                Description = "Path to the APK file",
                Required = true
            };

            Option<FileInfo> classDataOption = new("--classdata")
            {
                Description = "Path to the class data TPK file",
                DefaultValueFactory = _ => new FileInfo("./classdata.tpk")
            };

            Option<uint> portOption = new("--port")
            {
                Description = "Port number for the HTTP server",
                DefaultValueFactory = _ => 41669
            };

            Option<string> hostOption = new("--host")
            {
                Description = "Host for the HTTP server",
                DefaultValueFactory = _ => "127.0.0.1"
            };

            RootCommand rootCommand = new("PhiInfo HTTP Server CLI");
            rootCommand.Options.Add(apkOption);
            rootCommand.Options.Add(classDataOption);
            rootCommand.Options.Add(portOption);
            rootCommand.Options.Add(hostOption);

            using var exitEvent = new ManualResetEventSlim(false);

            rootCommand.SetAction(parseResult =>
            {
                FileInfo? apkFile = parseResult.GetValue(apkOption);
                FileInfo? classDataFile = parseResult.GetValue(classDataOption);
                uint port = parseResult.GetValue(portOption);
                string? host = parseResult.GetValue(hostOption);

                if (apkFile == null || !apkFile.Exists)
                {
                    Console.WriteLine($"Error: APK file not found: {apkFile?.FullName ?? "<null>"}");
                    return;
                }

                if (classDataFile == null || !classDataFile.Exists)
                {
                    Console.WriteLine($"Error: Class data file not found: {classDataFile?.FullName ?? "<null>"}");
                    return;
                }

                if (host == null)
                {
                    Console.WriteLine($"Error: Host is null");
                    return;
                }

                using var cldb = File.OpenRead(classDataFile.FullName);
                using var server = new CLIHttpServer(apkFile.FullName, cldb);

                _ = server.Start(port, host);

                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true; 
                    
                    Console.WriteLine("\n[System] Shutdown signal received.");
                    Console.WriteLine("[System] Stopping server...");
                    
                    server.Stop();
                    exitEvent.Set(); 
                };

                Console.WriteLine("--------------------------------------------");
                Console.WriteLine($"Server is running on http://{host}:{port}/");
                Console.WriteLine("Press Ctrl+C to stop the server.");
                Console.WriteLine("--------------------------------------------");

                exitEvent.Wait();

                Console.WriteLine("[System] Server stopped successfully.");
            });

            return rootCommand.Parse(args).Invoke();
        }
    }
}