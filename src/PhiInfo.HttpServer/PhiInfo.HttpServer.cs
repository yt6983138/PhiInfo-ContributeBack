using PhiInfo.Core;
using PhiInfo.Core.Type;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.Unicode;
using System.Threading;
using System.Threading.Tasks;

namespace PhiInfo
{
    [JsonSerializable(typeof(List<SongInfo>))]
    [JsonSerializable(typeof(List<Folder>))]
    [JsonSerializable(typeof(List<Avatar>))]
    [JsonSerializable(typeof(List<string>))]
    [JsonSerializable(typeof(List<ChapterInfo>))]
    public partial class JsonContext : JsonSerializerContext { }

    public abstract class HttpServer : IDisposable
    {
        private readonly JsonContext _jsonContext = new(new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        });

        private readonly PhiInfoAsset _phiAsset;
        private readonly Core.PhiInfo _phiInfo;
        private readonly ZipArchive _zip;
        private readonly string _phiVersion;

        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private bool _disposed;
        private readonly Dictionary<string, Func<HttpListenerRequest, Task<(byte[] data, string contentType)>>> _routeHandlers;

        public HttpServer(string apkPath, Stream cldbStream)
        {
            var apkFs = new FileStream(apkPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            _zip = new ZipArchive(apkFs, ZipArchiveMode.Read);

            _phiVersion = LoadVersionCode();

            var catalogEntry = GetEntrySafe("assets/aa/catalog.json");
            using var catalogStream = catalogEntry.Open();
            var catalogParser = new CatalogParser(catalogStream);

            _phiAsset = new PhiInfoAsset(catalogParser, (bundleName) =>
            {
                lock (_zip)
                {
                    var entry = GetEntrySafe("assets/aa/Android/" + bundleName);
                    return ExtractEntryToMemoryStream(entry);
                }
            });

            var files = SetupFiles(_zip);
            _phiInfo = new Core.PhiInfo(
                files.ggm,
                files.level0,
                files.level22,
                files.il2cppBytes,
                files.metadataBytes,
                cldbStream
            );

            const string octetStream = "application/octet-stream";
            const string jsonStream = "application/json";

            _routeHandlers = new()
            {
                ["/asset/text"] = async r => (GetAssetText(r.QueryString["path"]!), octetStream),
                ["/asset/music"] = async r => (GetAssetMusic(r.QueryString["path"]!), octetStream),
                ["/asset/image"] = async r => (GetAssetImage(r.QueryString["path"]!), octetStream),
                ["/info/songs"] = async _ => (SerializeJson(_phiInfo.ExtractSongInfo(), _jsonContext.ListSongInfo), jsonStream),
                ["/info/collection"] = async _ => (SerializeJson(_phiInfo.ExtractCollection(), _jsonContext.ListFolder), jsonStream),
                ["/info/avatars"] = async _ => (SerializeJson(_phiInfo.ExtractAvatars(), _jsonContext.ListAvatar), jsonStream),
                ["/info/tips"] = async _ => (SerializeJson(_phiInfo.ExtractTips(), _jsonContext.ListString), jsonStream),
                ["/info/chapters"] = async _ => (SerializeJson(_phiInfo.ExtractChapters(), _jsonContext.ListChapterInfo), jsonStream),
                ["/info/version"] = async _ => (Encoding.UTF8.GetBytes(_phiVersion), "text/plain")
            };
        }
        private async Task HandleRequest(HttpListenerContext context)
        {
            using var response = context.Response;
            try
            {
                string path = context.Request.Url?.AbsolutePath.ToLower() ?? "";
                
                response.Headers.Add("Access-Control-Allow-Origin", "*");

                if (_routeHandlers.TryGetValue(path, out var handler))
                {
                    var (data, contentType) = await handler(context.Request);
                    response.ContentType = contentType;
                    response.ContentLength64 = data.Length;
                    await response.OutputStream.WriteAsync(data);
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    byte[] msg = Encoding.UTF8.GetBytes("Endpoint not found");
                    await response.OutputStream.WriteAsync(msg);
                }
            }
            catch (Exception ex)
            {
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
                byte[] errorBuffer = Encoding.UTF8.GetBytes($"Server Error: {ex.Message}");
                try { await response.OutputStream.WriteAsync(errorBuffer); } catch { }
            }
        }

        private byte[] SerializeJson<T>(T data, JsonTypeInfo<T> typeInfo)
        {
            return JsonSerializer.SerializeToUtf8Bytes(data, typeInfo);
        }

        private byte[] GetAssetText(string? path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path is empty");
            var textData = _phiAsset.GetText(path);
            return Encoding.UTF8.GetBytes(textData.content);
        }

        private byte[] GetAssetMusic(string? path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path is empty");
            return _phiAsset.GetMusicRaw(path).data;
        }

        private byte[] GetAssetImage(string? path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Path is empty");
            return _phiAsset.GetImageRaw(path).WithHeader();
        }

        protected abstract string LoadVersionCode();
        protected virtual void Log(string msg)
        {
            Console.WriteLine(msg);
        }

        private struct Files
        {
            public Stream ggm;
            public Stream level0;
            public byte[] il2cppBytes;
            public byte[] metadataBytes;
            public Stream level22;
        }

        private static Files SetupFiles(ZipArchive zip)
        {
            Stream? ggm = null;
            Stream? level0 = null;
            byte[]? il2cppBytes = null;
            byte[]? metadataBytes = null;
            var level22Parts = new List<(int index, byte[] data)>();

            foreach (var entry in zip.Entries)
            {
                switch (entry.FullName)
                {
                    case "assets/bin/Data/globalgamemanagers.assets":
                        ggm = ExtractEntryToMemoryStream(entry);
                        break;
                    case "assets/bin/Data/level0":
                        level0 = ExtractEntryToMemoryStream(entry);
                        break;
                    case "lib/arm64-v8a/libil2cpp.so":
                        il2cppBytes = ExtractEntryToMemory(entry);
                        break;
                    case "assets/bin/Data/Managed/Metadata/global-metadata.dat":
                        metadataBytes = ExtractEntryToMemory(entry);
                        break;
                    default:
                        if (entry.FullName.StartsWith("assets/bin/Data/level22.split"))
                        {
                            string suffix = entry.FullName["assets/bin/Data/level22.split".Length..];
                            if (int.TryParse(suffix, out int index))
                                level22Parts.Add((index, ExtractEntryToMemory(entry)));
                        }
                        break;
                }
            }

            if (ggm == null || level0 == null || il2cppBytes == null || metadataBytes == null || level22Parts.Count == 0)
                throw new FileNotFoundException("Required Unity assets missing from APK");

            level22Parts.Sort((a, b) => a.index.CompareTo(b.index));
            MemoryStream level22 = new();
            foreach (var part in level22Parts)
                level22.Write(part.data, 0, part.data.Length);

            level22.Position = 0;

            return new Files
            {
                ggm = ggm,
                level0 = level0,
                il2cppBytes = il2cppBytes,
                metadataBytes = metadataBytes,
                level22 = level22,
            };
        }

        protected ZipArchiveEntry GetEntrySafe(string name) 
            => _zip.GetEntry(name) ?? throw new FileNotFoundException($"Entry not found: {name}");

        private static byte[] ExtractEntryToMemory(ZipArchiveEntry entry)
        {
            using var ms = new MemoryStream((int)entry.Length);
            using var s = entry.Open();
            s.CopyTo(ms);
            return ms.ToArray();
        }

        private static MemoryStream ExtractEntryToMemoryStream(ZipArchiveEntry entry)
        {
            var ms = new MemoryStream((int)entry.Length);
            using (var s = entry.Open())
            {
                s.CopyTo(ms);
            }
            ms.Position = 0;
            return ms;
        }

        public async Task Start(uint port = 41669, string host = "localhost")
        {
            if (_listener?.IsListening == true) return;

            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://{host}:{port}/");
            
            _cts = new CancellationTokenSource();
            _listener.Start();
            await Task.Run(() => ListenLoop(_cts.Token));
        }

        private async Task ListenLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener?.IsListening == true)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context), token);
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
                catch (Exception ex)
                {
                    Log($"[HttpServer] Accept Error: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            _cts?.Cancel();
            if (_listener?.IsListening == true)
            {
                _listener.Stop();
                _listener.Close();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            Stop();
            _cts?.Dispose();
            _zip?.Dispose();
            _phiInfo?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}