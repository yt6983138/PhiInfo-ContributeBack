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
using PhiInfo.Processing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using JsonContext = PhiInfo.Processing.JsonContext;

namespace PhiInfo.CLI;

internal static class Export
{
    private static readonly JsonContext JsonContext = new(new JsonSerializerOptions
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    });

    private static readonly Option<string> OutputOption = new("--output")
    {
        Aliases = { "-o" },
        Description = "Output directory",
        DefaultValueFactory = _ => "./output"
    };

    private static readonly Option<string> ImageFormatOption = new("--image-format")
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

    public static readonly Command Command = new("export", "Run export mode commands")
    {
        Options =
        {
            OutputOption,
            ImageFormatOption
        },
        Action = new CommandLineAction(HandleCommand)
    };

    private static int HandleCommand(ParseResult parseResult)
    {
        var manager = Configuration.Default.ImageFormatsManager;
        var output = parseResult.GetValue(OutputOption)!;
        var format = parseResult.GetValue(ImageFormatOption)!;
        RunExportMode(Program.GetContext(parseResult), output, manager.FindByName(format)!);
        return 0;
    }

    private static void RunExportMode(PhiInfoContext context, string localOutput, IImageFormat format)
    {
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

        Console.WriteLine("OK");
    }

    private static void ExtractAsset(
        PhiInfoContext context,
        string assetDir,
        string outputPath,
        string assetId,
        IImageFormat format)
    {
        var manager = Configuration.Default.ImageFormatsManager;
        var fullOutputPath = Path.Combine(assetDir, outputPath);

        void EnsureDir()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
        }

        if (outputPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            EnsureDir();

            using var textData = context.Bundle.Get<UnityText>(assetId);
            File.WriteAllText(fullOutputPath, textData.Content);
        }
        else if (outputPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            EnsureDir();

            var rawMusic = context.Bundle.Get<UnityMusic>(assetId);
            var musicData = PhiInfoDecoders.DecoderMusic(rawMusic);
            File.WriteAllBytes(fullOutputPath, musicData);
        }
        else if (outputPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
        {
            EnsureDir();

            using var file = File.OpenWrite(fullOutputPath + "." + format.FileExtensions.First());
            using var image = PhiInfoDecoders.DecoderImage(context.Bundle.Get<UnityImage>(assetId));
            image.Save(file, manager.GetEncoder(format));
        }
        else if (outputPath.StartsWith("avatar.", StringComparison.OrdinalIgnoreCase))
        {
            EnsureDir();

            using var file = File.OpenWrite(fullOutputPath + "." + format.FileExtensions.First());
            using var image = PhiInfoDecoders.DecoderImage(context.Bundle.Get<UnityImage>(assetId));
            image.Save(file, manager.GetEncoder(format));
        }
    }
}