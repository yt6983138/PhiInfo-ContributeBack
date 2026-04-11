using System;
using System.CommandLine;
using System.CommandLine.Completions;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using PhiInfo.Core;
using PhiInfo.Core.Asset;
using PhiInfo.Core.Type;
using PhiInfo.Processing;
using PhiInfo.Processing.DataProvider;
using Shua.Zip;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using JsonContext = PhiInfo.Processing.JsonContext;

namespace PhiInfo.CLI;

internal static class Local
{
    private static readonly JsonContext JsonContext = new(new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    });

    public static Command CreateCommand(
        Option<IReadAt[]> packagesOption,
        Option<FileInfo> classDataOption,
        Option<Language> langOption)
    {
        var outputOption = new Option<string>("--output")
        {
            Aliases = { "-o" },
            Description = "Output directory",
            DefaultValueFactory = _ => "./output"
        };

        var imageFormatOption = new Option<string>("--image-format")
        {
            Aliases = { "-if" },
            Description = "Image Format",
            DefaultValueFactory = _ => "JPEG",
            CompletionSources =
            {
                _ =>
                {
                    return Configuration.Default.ImageFormatsManager.ImageFormats
                        .Select(v => new CompletionItem(v.Name));
                }
            },
            CustomParser = result =>
            {
                var manager = Configuration.Default.ImageFormatsManager;

                var value = result.Tokens.Single().Value;

                var format = manager.FindByName(value);
                if (format == null)
                {
                    result.AddError($"Unknown format: {value}");
                    return null;
                }

                return format.Name;
            }
        };

        var command = new Command("local", "Run local extraction mode");
        command.Options.Add(outputOption);
        command.Options.Add(imageFormatOption);
        command.SetAction(parseResult =>
        {
            var manager = Configuration.Default.ImageFormatsManager;
            var packages = parseResult.GetValue(packagesOption)!;
            var classData = parseResult.GetValue(classDataOption)!;
            var output = parseResult.GetValue(outputOption)!;
            var lang = parseResult.GetValue(langOption);
            var format = parseResult.GetValue(imageFormatOption)!;

            if (!Program.ValidateCommonOptions(packages, classData))
                return;

            var shuaZips = packages.Select(p => new ShuaZip(p)).ToArray();
            var dataProvider = new AndroidPackagesDataProvider(shuaZips, File.OpenRead(classData.FullName));

            RunLocalMode(dataProvider, output, lang, manager.FindByName(format)!);
        });
        return command;
    }

    public static void RunLocalMode(AndroidPackagesDataProvider dataProvider, string localOutput, Language lang,
        IImageFormat format)
    {
        using var context = new PhiInfoContext(dataProvider, lang);

        Directory.CreateDirectory(localOutput);

        var allInfo = context.Info.ExtractAllInfo();
        var allInfoJson = JsonSerializer.Serialize(allInfo, JsonContext.AllInfo);
        File.WriteAllText(Path.Combine(localOutput, "all_info.json"), allInfoJson);

        var assetDir = Path.Combine(localOutput, "asset");
        Directory.CreateDirectory(assetDir);

        var assets = context.Catalog.GetAll()
            .Where(v => v.Key.IsString && v.Value != null && v.Value.Value.IsString)
            .Select(v => new { outputPath = v.Key.Str!, assetId = v.Value!.Value.Str! })
            .ToList();

        var tasks = assets
            .Select(asset => Task.Run(() => ExtractAsset(context, assetDir, asset.outputPath, asset.assetId, format)))
            .ToArray();
        Task.WaitAll(tasks);

        Console.WriteLine("Local extraction completed.");
    }

    private static void ExtractAsset(PhiInfoContext context, string assetDir, string outputPath, string assetId,
        IImageFormat format)
    {
        var manager = Configuration.Default.ImageFormatsManager;
        var fullOutputPath = Path.Combine(assetDir, outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);

        if (outputPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            using var textData = context.Bundle.Get<UnityText>(assetId);
            File.WriteAllText(fullOutputPath, textData.Content);
        }
        else if (outputPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            var rawMusic = context.Bundle.Get<UnityMusic>(assetId);
            var musicData = PhiInfoDecoders.DecoderMusic(rawMusic);
            File.WriteAllBytes(fullOutputPath, musicData);
        }
        else if (outputPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
        {
            using var file = File.OpenWrite(fullOutputPath + "." + format.FileExtensions.First());
            using var image = PhiInfoDecoders.DecoderImage(context.Bundle.Get<UnityImage>(assetId));
            image.Save(file, manager.GetEncoder(format));
        }
        else if (outputPath.StartsWith("avatar.", StringComparison.OrdinalIgnoreCase))
        {
            using var file = File.OpenWrite(fullOutputPath + "." + format.FileExtensions.First());
            using var image = PhiInfoDecoders.DecoderImage(context.Bundle.Get<UnityImage>(assetId));
            image.Save(file, manager.GetEncoder(format));
        }
    }
}