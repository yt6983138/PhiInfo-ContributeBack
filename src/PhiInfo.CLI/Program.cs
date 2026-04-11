using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Reflection;
using PhiInfo.Core.Type;
using PhiInfo.Processing.Type;
using Shua.Zip;
using Shua.Zip.ReadAt;

namespace PhiInfo.CLI;

internal class Program
{
    internal static AppInfo GetAppInfo()
    {
        var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "Unknown";
        return new AppInfo(version, "CLI");
    }

    internal static IReadAt CreateReadAt(string path)
    {
        if (path.StartsWith("http://") || path.StartsWith("https://")) return new HttpReadAt(path);

        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
            throw new FileNotFoundException($"Package file not found: {path}");
        return new MmapReadAt(path);
    }

    internal static bool ValidateCommonOptions(IReadAt[] packages, FileInfo classDataFile)
    {
        if (packages.Length == 0)
        {
            Console.WriteLine("Error: No packages provided");
            return false;
        }

        if (!classDataFile.Exists)
        {
            Console.WriteLine($"Error: Class data file not found: {classDataFile.FullName}");
            return false;
        }

        return true;
    }

    private static int Main(string[] args)
    {
        Option<IReadAt[]> packagesOption = new("--package")
        {
            Aliases = { "-p" },
            Description = """
                          Path to package files or URLs. A package file can be APK, main OBB, or patch OBB. 
                          If your copy of Phigros is downloaded from Google play require all of those 
                          or the first two files.
                          If your copy of Phigros is downloaded from TapTap, you only need to provide 
                          the APK file, since TapTap's APK already contains all the data.
                          """,
            Required = true,
            CustomParser = result =>
            {
                try
                {
                    var readAts = result.Tokens
                        .Select(token => CreateReadAt(token.Value))
                        .ToArray();
                    return readAts;
                }
                catch (Exception ex)
                {
                    result.AddError($"Error parsing package: {ex.Message}");
                    return null;
                }
            }
        };

        Option<FileInfo> classDataOption = new("--classdata")
        {
            Aliases = { "-cldb" },
            Description = "Path to the class data TPK file",
            DefaultValueFactory = _ => new FileInfo("./classdata.tpk")
        };

        Option<Language> langOption = new("--language")
        {
            Aliases = { "-l", "--lang" },
            Description = "Default language",
            DefaultValueFactory = _ => Language.Chinese
        };

        // Root command
        RootCommand rootCommand = new("PhiInfo CLI");
        rootCommand.Options.Add(packagesOption);
        rootCommand.Options.Add(classDataOption);
        rootCommand.Options.Add(langOption);
        rootCommand.Add(HttpServer.CreateCommand(packagesOption, classDataOption, langOption));
        rootCommand.Add(Local.CreateCommand(packagesOption, classDataOption, langOption));

        return rootCommand.Parse(args).Invoke();
    }
}