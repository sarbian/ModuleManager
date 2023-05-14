using System;
using System.IO;
using System.Security.Cryptography;
using ModuleManager.Extensions;

namespace ModuleManager.Utils
{
    public static class FileUtils
    {
        public static string FileSHA(string filename)
        {
            if (!File.Exists(filename)) throw new FileNotFoundException("File does not exist", filename);

            using SHA256 sha = SHA256.Create();
            using FileStream fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            byte[] data = sha.ComputeHash(fs);

            return data.ToHex();
        }
    }
}
