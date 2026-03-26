using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using Shua.Zip.Type;

namespace Shua.Zip
{
    public sealed class MmapReadAt : IReadAt, IDisposable
    {
        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;

        public long Size { get; }

        public MmapReadAt(string filePath)
        {
            if (filePath == null) throw new ArgumentNullException(nameof(filePath));

            var fileInfo = new FileInfo(filePath);
            Size = fileInfo.Length;

            _mmf = MemoryMappedFile.CreateFromFile(
                filePath,
                FileMode.Open,
                mapName: null,
                capacity: 0,
                MemoryMappedFileAccess.Read);

            _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        }

        public Stream OpenRead(long offset, int length)
        {
            if (offset < 0 || offset + length > Size)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset out of range");

            return _mmf.CreateViewStream(offset, length, MemoryMappedFileAccess.Read);
        }

        public void Dispose()
        {
            _accessor?.Dispose();
            _mmf?.Dispose();
        }
    }
}
