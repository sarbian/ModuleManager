using System;

namespace ModuleManager.Extensions
{
    public static class ByteArrayExtensions
    {
        public static string ToHex(this byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            char[] result = new char[data.Length * 2];

            for (int i = 0; i < data.Length; i++)
            {
                result[i * 2] = GetHexValue(data[i] / 16);
                result[i * 2 + 1] = GetHexValue(data[i] % 16);
            }

            return new string(result);
        }

        private static char GetHexValue(int i)
        {
            if (i < 10)
                return (char)(i + '0');
            else
                return (char)(i - 10 + 'a');
        }
    }
}
