using System;

namespace ModuleManager.Extensions
{
    public static class UrlFileExtensions
    {
        public static string GetUrlWithExtension(this UrlDir.UrlFile urlFile)
        {
            return $"{urlFile.url}.{urlFile.fileExtension}";
        }
        public static string GetNameWithExtension(this UrlDir.UrlFile urlFile)
        {
            return $"{urlFile.name}.{urlFile.fileExtension}";
        }
    }
}
