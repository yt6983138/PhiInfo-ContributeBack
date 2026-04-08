#pragma warning disable IDE1006
#pragma warning disable IDE0130

using System;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using PhiInfo.Core.Asset.Type;

namespace PhiInfo.Core.Asset;

public record struct UnityImage(uint format, uint width, uint height, Stream data)
    : IFromBundle<UnityImage>, IDisposable
{
    private readonly MappedAssetBundle _bundle;

    private UnityImage(uint format, uint width, uint height, Stream data, MappedAssetBundle bundle)
        : this(format, width, height, data)
    {
        _bundle = bundle;
    }

    public void Dispose()
    {
        _bundle.Dispose();
    }

    public static UnityImage FromBundle(MappedAssetBundle bundle)
    {
        var field = bundle.FindAssetField(AssetClassID.Texture2D) ??
                    throw new ArgumentException("No Texture2D found.", nameof(bundle));

        var width = field["m_Width"].AsUInt;
        var height = field["m_Height"].AsUInt;
        var format = field["m_TextureFormat"].AsUInt;

        var dataOffset = field["m_StreamData"]["offset"].AsLong;
        var size = field["m_StreamData"]["size"].AsLong;

        bundle.BundleFile.GetFileRange(1, out var dataFileOffset, out _);
        var offset = dataFileOffset + dataOffset;
        var data = new SegmentStream(bundle.BundleFile.DataReader.BaseStream, offset, size);

        return new UnityImage(format, width, height, data, bundle);
    }
}