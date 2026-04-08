#pragma warning disable IDE1006
#pragma warning disable IDE0130

using System;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using PhiInfo.Core.Asset.Type;

namespace PhiInfo.Core.Asset;

public record struct UnityMusic(float length, Stream data) : IFromBundle<UnityMusic>, IDisposable
{
    private readonly MappedAssetBundle _bundle;

    private UnityMusic(float length, Stream data, MappedAssetBundle bundle)
        : this(length, data)
    {
        _bundle = bundle;
    }

    public void Dispose()
    {
        _bundle.Dispose();
    }

    public static UnityMusic FromBundle(MappedAssetBundle bundle)
    {
        var field = bundle.FindAssetField(AssetClassID.AudioClip) ??
                    throw new ArgumentException("No AudioClip found.", nameof(bundle));

        var dataOffset = field["m_Resource"]["m_Offset"].AsLong;
        var size = field["m_Resource"]["m_Size"].AsLong;
        var length = field["m_Length"].AsFloat;

        bundle.BundleFile.GetFileRange(1, out var dataFileOffset, out _);
        var offset = dataFileOffset + dataOffset;
        var data = new SegmentStream(bundle.BundleFile.DataReader.BaseStream, offset, size);

        return new UnityMusic(length, data, bundle);
    }
}