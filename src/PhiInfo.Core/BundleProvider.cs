using AssetsTools.NET;
using AssetsTools.NET.Extra;
using PhiInfo.Core.Asset.Type;
using PhiInfo.Core.Type;

namespace PhiInfo.Core;

public class BundleProvider(IAssetDataProvider dataProvider)
{
    private MappedAssetBundle LoadBundle(string name)
    {
        var file = dataProvider.GetBundle(name);

        var reader = new AssetsFileReader(file);
        var bundleFile = new AssetBundleFile();
        bundleFile.Read(reader);

        if (bundleFile.DataIsCompressed)
            bundleFile = BundleHelper.UnpackBundle(bundleFile);

        bundleFile.GetFileRange(0, out var offset, out var size);

        var stream = new SegmentStream(bundleFile.DataReader.BaseStream, offset, size);

        var infoFile = new AssetsFile();
        infoFile.Read(stream);

        return new MappedAssetBundle(bundleFile, infoFile);
    }

    public T Get<T>(string name)
        where T : IFromBundle<T>
    {
        return T.FromBundle(LoadBundle(name));
    }
}