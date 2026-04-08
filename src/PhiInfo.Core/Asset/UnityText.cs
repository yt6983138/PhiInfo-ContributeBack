#pragma warning disable IDE1006
#pragma warning disable IDE0130

using System;
using AssetsTools.NET.Extra;
using PhiInfo.Core.Asset.Type;

namespace PhiInfo.Core.Asset;

public record struct UnityText(string content) : IFromBundle<UnityText>, IDisposable
{
    private readonly MappedAssetBundle _bundle;

    private UnityText(string content, MappedAssetBundle bundle)
        : this(content)
    {
        _bundle = bundle;
    }

    public void Dispose()
    {
        _bundle.Dispose();
    }

    public static UnityText FromBundle(MappedAssetBundle bundle)
    {
        var field = bundle.FindAssetField(AssetClassID.TextAsset) ??
                    throw new ArgumentException("No TextAsset found.", nameof(bundle));

        return new UnityText(field["m_Script"].AsString, bundle);
    }
}