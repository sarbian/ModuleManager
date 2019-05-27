using System;
using System.IO;
using System.Security.Cryptography;

namespace ModuleManager.Utils
{
    public interface IFileShaGenerator
    {
        void TransformBlock(SHA256 sha, IReadableObject readableObject);
        string ComputeSha(IReadableObject readableObject);
    }

    public class FileShaGenerator
    {
        public void TransformBlock(SHA256 sha, IReadableObject readableObject)
        {
            if (sha == null) throw new ArgumentNullException(nameof(sha));
            if (readableObject == null) throw new ArgumentNullException(nameof(readableObject));

            byte[] contentBytes = readableObject.ReadAllBytes();
            sha.TransformBlock(contentBytes, 0, contentBytes.Length, contentBytes, 0);
        }

        public string ComputeSha(IReadableObject readableObject)
        {
            if (readableObject == null) throw new ArgumentNullException(nameof(readableObject));

            byte[] shaBytes = null;

            using (SHA256 sha = SHA256.Create())
            {
                using (Stream stream = readableObject.OpenRead())
                {
                    shaBytes = sha.ComputeHash(stream);
                }
            }

            char[] result = new char[shaBytes.Length * 2];

            for (int i = 0; i < shaBytes.Length; i++)
            {
                result[i * 2] = GetHexValue(shaBytes[i] / 16);
                result[i * 2 + 1] = GetHexValue(shaBytes[i] % 16);
            }

            return new string(result);
        }

        private static char GetHexValue(int i) {
            if (i < 10)
                return (char)(i + '0');
            else
                return (char)(i - 10 + 'a');
        }
    }
}
