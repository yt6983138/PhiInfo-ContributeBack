using System;
using AssetRipper.TextureDecoder.Etc;
using AssetRipper.TextureDecoder.Rgb.Formats;
using Fmod5Sharp;
using Fmod5Sharp.CodecRebuilders;
using PhiInfo.Core.Asset;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PhiInfo.Processing;

public static class PhiInfoDecoders
{
    public static byte[] DecoderMusic(UnityMusic raw)
    {
        var bank = FsbLoader.LoadFsbFromStream(raw.data);
        var music = FmodVorbisRebuilder.RebuildOggFile(bank.Samples[0]);
        raw.Dispose();
        return music;
    }

    private static Image LoadImage(UnityImage raw)
    {
        var bytes = new byte[raw.data.Length];
        raw.data.ReadExactly(bytes, 0, bytes.Length);
        raw.Dispose();
        switch (raw.format)
        {
            case 3:
                return Image.LoadPixelData<Rgb24>(bytes, (int)raw.width, (int)raw.height);

            case 4:
                return Image.LoadPixelData<Rgba32>(bytes, (int)raw.width, (int)raw.height);

            case 34:
            {
                EtcDecoder.DecompressETC<ColorBGRA<byte>, byte>(
                    bytes, (int)raw.width, (int)raw.height, out var data);
                return Image.LoadPixelData<Bgra32>(data, (int)raw.width, (int)raw.height);
            }

            case 47:
            {
                EtcDecoder.DecompressETC2A8<ColorBGRA<byte>, byte>(
                    bytes, (int)raw.width, (int)raw.height, out var data);
                return Image.LoadPixelData<Bgra32>(data, (int)raw.width, (int)raw.height);
            }

            default:
                throw new NotSupportedException($"Unknown format: {raw.format}");
        }
    }

    public static Image DecoderImage(UnityImage raw)
    {
        var img = LoadImage(raw);
        img.Mutate(x => x.Flip(FlipMode.Vertical));
        return img;
    }
}