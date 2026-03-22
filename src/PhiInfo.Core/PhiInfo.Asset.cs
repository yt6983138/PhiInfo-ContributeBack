using System;
using System.Collections.Generic;
using System.IO;
using AssetsTools.NET;
using AssetsTools.NET.Extra;
using PhiInfo.Core.Type;

namespace PhiInfo.Core;

public class PhiInfoAsset(CatalogParser catalogParser, Func<string, Stream> getBundleStreamFunc)
{
    private readonly CatalogParser _catalogParser = catalogParser;

    private readonly Func<string, Stream> _getBundleStreamFunc = getBundleStreamFunc;

    static byte[] ReadRangeAsBytes(Stream baseStream, long offset, int size)
    {
        byte[] buffer = new byte[size];

        long oldPos = baseStream.Position;
        try
        {
            baseStream.Seek(offset, SeekOrigin.Begin);

            int readTotal = 0;
            while (readTotal < size)
            {
                int read = baseStream.Read(
                    buffer,
                    readTotal,
                    size - readTotal
                );

                if (read == 0)
                    throw new EndOfStreamException();

                readTotal += read;
            }
        }
        finally
        {
            baseStream.Position = oldPos;
        }

        return buffer;
    }

    private T ProcessAssetBundle<T>(string path, Func<AssetBundleFile, AssetsFile, T> processor)
    {
        var file = getBundle(path);
        var reader = new AssetsFileReader(file);
        AssetBundleFile bun = new();
        bun.Read(reader);
        if (bun.DataIsCompressed)
        {
            bun = BundleHelper.UnpackBundle(bun);
        }

        bun.GetFileRange(0, out long offset, out long size);
        SegmentStream stream = new(bun.DataReader.BaseStream, offset, size);
        AssetsFile info_file = new();
        info_file.Read(new AssetsFileReader(stream));

        try
        {
            return processor(bun, info_file);
        }
        finally
        {
            bun.Close();
            info_file.Close();
        }
    }

    public Image GetImageRaw(string path)
    {
        return ProcessAssetBundle(path, (bun, info_file) =>
        {
            foreach (var info in info_file.AssetInfos)
            {
                if (info.TypeId == (int)AssetClassID.Texture2D)
                {
                    var baseField = PhiInfo.GetBaseField(info_file, info);
                    var height = baseField["m_Height"].AsUInt;
                    var width = baseField["m_Width"].AsUInt;
                    var format = baseField["m_TextureFormat"].AsUInt;
                    var data_offset = baseField["m_StreamData"]["offset"].AsLong;
                    var data_size = baseField["m_StreamData"]["size"].AsLong;
                    bun.GetFileRange(1, out long data_file_offset, out long data_file_size);
                    var data = ReadRangeAsBytes(bun.DataReader.BaseStream, data_file_offset + data_offset, (int)data_size);
                    var image = new Image { format = format, width = width, height = height, data = data };
                    return image;
                }
            }
            throw new Exception("No Texture2D found in the asset bundle.");
        });
    }

    public Music GetMusicRaw(string path)
    {
        return ProcessAssetBundle(path, (bun, info_file) =>
        {
            foreach (var info in info_file.AssetInfos)
            {
                if (info.TypeId == (int)AssetClassID.AudioClip)
                {
                    var baseField = PhiInfo.GetBaseField(info_file, info);
                    var data_offset = baseField["m_Resource"]["m_Offset"].AsLong;
                    var data_size = baseField["m_Resource"]["m_Size"].AsLong;
                    var length = baseField["m_Length"].AsFloat;
                    bun.GetFileRange(1, out long data_file_offset, out long data_file_size);
                    var data = ReadRangeAsBytes(bun.DataReader.BaseStream, data_file_offset + data_offset, (int)data_size);
                    return new Music { data = data, length = length };
                }
            }
            throw new Exception("No AudioClip found in the asset bundle.");
        });
    }

    public Text GetText(string path)
    {
        return ProcessAssetBundle(path, (bun, info_file) =>
        {
            foreach (var info in info_file.AssetInfos)
            {
                if (info.TypeId == (int)AssetClassID.TextAsset)
                {
                    var baseField = PhiInfo.GetBaseField(info_file, info);
                    var text = baseField["m_Script"].AsString;
                    return new Text { content = text };
                }
            }
            throw new Exception("No TextAsset found in the asset bundle.");
        });
    }

    private Stream getBundle(string path)
    {
        var bundlePath = _catalogParser.Get(path);
        if (bundlePath == null)
            throw new Exception($"Asset {path} not found in catalog.");
        if (bundlePath.Value.ResolvedKey == null)
            throw new Exception($"Asset {path} has no resolved bundle path.");
        if (bundlePath.Value.ResolvedKey.Value.StringValue == null)
            throw new Exception($"Asset {path} has invalid resolved bundle path.");

        return _getBundleStreamFunc(bundlePath.Value.ResolvedKey.Value.StringValue);
    }
}