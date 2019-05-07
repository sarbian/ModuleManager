using System;
using System.Linq;

namespace ModuleManager.Extensions
{
    public static class UrlDirExtensions
    {
        public static UrlDir.UrlFile FindFile(this UrlDir urlDir, string url, UrlDir.FileType? fileType = null)
        {
            if (urlDir == null) throw new ArgumentNullException(nameof(urlDir));
            if (url == null) throw new ArgumentNullException(nameof(url));
            string[] splits = url.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            UrlDir currentDir = urlDir;

            for (int i = 0; i < splits.Length - 1; i++)
            {
                currentDir = currentDir.children.FirstOrDefault(subDir => subDir.name == splits[i]);
                if (currentDir == null) return null;
            }

            string fileName = splits[splits.Length - 1];
            string fileExtension = null;

            int idx = fileName.LastIndexOf('.');

            if (idx > -1)
            {
                fileExtension = fileName.Substring(idx + 1);
                fileName = fileName.Substring(0, idx);
            }

            foreach (UrlDir.UrlFile file in currentDir.files)
            {
                if (file.name != fileName) continue;
                if (fileExtension != null && fileExtension != file.fileExtension) continue;
                if (fileType != null && file.fileType != fileType) continue;
                return file;
            }

            return null;
        }
    }
}
