#pragma warning disable IDE1006
#pragma warning disable IDE0130

using System;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace PhiInfo.Core.Asset.Type;

public readonly record struct MappedAssetBundle(
    AssetBundleFile BundleFile,
    AssetsFile InfoAssetFile) : IDisposable
{
    public void Dispose()
    {
        BundleFile.Close();
        InfoAssetFile.Close();
    }

    public AssetTypeValueField? FindAssetField(AssetClassID type)
    {
        foreach (var info in InfoAssetFile.AssetInfos)
            if (info.TypeId == (int)type)
                return InfoAssetFile.GetBaseField(info);
        return null;
    }
}

public interface IFromBundle<out TSelf>
    where TSelf : IFromBundle<TSelf>
{
    static abstract TSelf FromBundle(MappedAssetBundle bundle);
}