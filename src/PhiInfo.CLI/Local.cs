using System;
using System.IO;
using System.Linq;
using System.Text.Json;
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
    public static void RunLocalMode(FileInfo[] packages, FileInfo classDataFile, string localOutput, Language lang,
        IImageFormat format)
    {
        var dataProvider = new AndroidPackagesDataProvider(
            packages.Select(p => new ShuaZip(new MmapReadAt(p.FullName))).ToArray(),
            File.OpenRead(classDataFile.FullName));

        using var context = new PhiInfoContext(dataProvider, lang);

        Directory.CreateDirectory(localOutput);

        var allInfo = context.Info.ExtractAllInfo();
        var allInfoJson = JsonSerializer.Serialize(allInfo, JsonContext.Default.AllInfo);
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
            File.WriteAllText(fullOutputPath, textData.content);
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