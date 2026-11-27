using System;
using System.Globalization;
using System.Threading;

namespace ModuleManagerTests
{
    public sealed class TemporaryCulture : IDisposable
    {
        // Lowercase i and uppercase I are not a case pair in this culture
        public const string TURKISH = "tr-TR";

        private readonly CultureInfo originalCulture;

        public TemporaryCulture(string name)
        {
            originalCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo(name);
        }

        public void Dispose()
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }
    }
}
