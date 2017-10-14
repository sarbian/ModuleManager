using System;
using System.IO;

namespace ModuleManager.Utils
{
    public static class FileUtils
    {
        public static string FileSHA(string filename)
        {
            if (!File.Exists(filename)) throw new FileNotFoundException("File does not exist", filename);

            System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();

            byte[] data = null;
            using (FileStream fs = File.Open(filename, FileMode.Open, FileAccess.Read))
            {
                data = sha.ComputeHash(fs);
            }

            string hashedValue = string.Empty;

            foreach (byte b in data)
            {
                hashedValue += String.Format("{0,2:x2}", b);
            }

            return hashedValue;
        }
    }
}
