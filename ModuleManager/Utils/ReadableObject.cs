using System;
using System.IO;

namespace ModuleManager.Utils
{
    public interface IReadableObject
    {
        long Length { get; }
        Stream OpenRead();
    }

    public static class ReadableObjectExtensions
    {
        public static byte[] ReadAllBytes(this IReadableObject readableObject)
        {
            if (readableObject == null) throw new ArgumentNullException(nameof(readableObject));

            long fileLength = readableObject.Length;
            if (fileLength > Int32.MaxValue)
                throw new IOException($"Object is too large to be read (should be less than 2 GB): {readableObject}");

            byte[] bytes;
            using(Stream stream = readableObject.OpenRead())
            {
                int count = (int) fileLength;
                int index = 0;
                bytes = new byte[count];
                while(count > 0)
                {
                    int nBytesRead = stream.Read(bytes, index, count);
                    if (nBytesRead == 0)
                        throw new EndOfStreamException("Read beyond the end of the file, this shouldn't be possible!");
                    index += nBytesRead;
                    count -= nBytesRead;
                }
            }
            return bytes;
        }
    }

    public class ReadableObjectFile : IReadableObject
    {
        private readonly FileInfo fileInfo;

        public ReadableObjectFile(FileInfo fileInfo)
        {
            this.fileInfo = fileInfo ?? throw new ArgumentNullException(nameof(fileInfo));
        }

        public long Length => fileInfo.Length;

        public Stream OpenRead() => fileInfo.OpenRead();

        // public static implicit operator ReadableObjectFile(FileInfo fileInfo) => new ReadableObjectFile(fileInfo);
    }
}
