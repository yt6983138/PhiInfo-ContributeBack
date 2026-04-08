using System;
using System.Linq;
using SixLabors.ImageSharp.Formats;

namespace PhiInfo.CLI;

public static class Extensions
{
    public static IImageFormat? FindByName(this ImageFormatManager manager, string name)
    {
        return manager.ImageFormats.FirstOrDefault(f =>
            string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}