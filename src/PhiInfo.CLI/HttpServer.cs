using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading;
using PhiInfo.Core.Type;
using PhiInfo.Processing;
using PhiInfo.Processing.DataProvider;
using PhiInfo.Processing.Type;
using Shua.Zip;

namespace PhiInfo.CLI;

internal static class HttpServer
{
    public static Command CreateCommand(
        Option<IReadAt[]> packagesOption,
        Option<FileInfo> classDataOption,
        Option<Language> langOption)
    {
        var portOption = new Option<uint>("--port")
        {
            Description = "Port number for the HTTP server",
            DefaultValueFactory = _ => 41669
        };

        var hostOption = new Option<string>("--host")
        {
            Description = "Host for the HTTP server",
            DefaultValueFactory = _ => "127.0.0.1"
        };

        var command = new Command("server", "Run HTTP server mode");
        command.Options.Add(portOption);
        command.Options.Add(hostOption);
        command.SetAction(parseResult =>
        {
            var packages = parseResult.GetValue(packagesOption)!;
            var classData = parseResult.GetValue(classDataOption)!;
            var port = parseResult.GetValue(portOption);
            var host = parseResult.GetValue(hostOption)!;
            var lang = parseResult.GetValue(langOption);

            if (!Program.ValidateCommonOptions(packages, classData))
                return;

            var shuaZips = packages.Select(p => new ShuaZip(p)).ToArray();
            var dataProvider = new AndroidPackagesDataProvider(shuaZips, File.OpenRead(classData.FullName));

            RunServerMode(dataProvider, port, host, lang, Program.GetAppInfo);
        });
        return command;
    }

    public static void RunServerMode(AndroidPackagesDataProvider dataProvider, uint port, string host, Language lang,
        Func<AppInfo> getAppInfo)
    {
        using var exitEvent = new ManualResetEventSlim(false);

        using var server = new PhiInfoHttpServer(dataProvider, getAppInfo(), port, host, lang);

        server.OnRequestError += (sender, ex) => { Console.WriteLine($"Server error: {ex}"); };

        // 注册事件
        Console.CancelKeyPress += OnCancelKeyPress;

        Console.WriteLine("--------------------------------------------");
        Console.WriteLine($"Server is running on http://{host}:{port}/");
        Console.WriteLine("Press Ctrl+C to stop the server.");
        Console.WriteLine("--------------------------------------------");

        exitEvent.Wait();

        Console.WriteLine("[System] Server stopped successfully.");
        return;

        void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;

            Console.WriteLine("\n[System] Shutdown signal received.");
            Console.WriteLine("[System] Stopping server...");

            exitEvent.Set();
        }
    }
}