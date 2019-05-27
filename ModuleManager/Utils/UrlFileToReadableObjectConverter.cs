using System;
using System.IO;

namespace ModuleManager.Utils
{
    public interface IUrlFileToReadableObjectConverter
    {
        IReadableObject ConvertToReadableObject(UrlDir.UrlFile urlFile);
    }

    public class UrlFileToReadableObjectConverter : IUrlFileToReadableObjectConverter
    {
        public IReadableObject ConvertToReadableObject(UrlDir.UrlFile urlFile)
        {
            if (urlFile == null) throw new ArgumentNullException(nameof(urlFile));
            return new ReadableObjectFile(new FileInfo(urlFile.fullPath));
        }
    }
}
