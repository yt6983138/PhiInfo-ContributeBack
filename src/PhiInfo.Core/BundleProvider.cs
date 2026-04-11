using PhiInfo.Core.Asset;
using PhiInfo.Core.Type;

namespace PhiInfo.Core;

public class BundleProvider(IBundleDataProvider dataProvider)
{
    public T Get<T>(string name)
        where T : UnityAsset, new()
    {
        var obj = new T();
        obj.Init(dataProvider.GetBundle(name));
        return obj;
    }
}