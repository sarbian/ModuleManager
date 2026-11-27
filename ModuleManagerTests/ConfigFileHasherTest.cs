using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using ModuleManager;

namespace ModuleManagerTests
{
    public class ConfigFileHasherTest : IDisposable
    {
        private readonly string dir = Path.Combine(Path.GetTempPath(), "ConfigFileHasherTest-" + Guid.NewGuid().ToString("N"));

        public ConfigFileHasherTest()
        {
            Directory.CreateDirectory(dir);
        }

        public void Dispose()
        {
            Directory.Delete(dir, true);
        }

        [Fact]
        public void TestWaitForHash()
        {
            string[] paths = new string[200];
            for (int i = 0; i < paths.Length; i++)
            {
                paths[i] = Path.Combine(dir, i + ".cfg");
                File.WriteAllText(paths[i], new string('x', i * 37) + i);
            }

            using SHA256 sha = SHA256.Create();
            using (ConfigFileHasher hasher = new ConfigFileHasher(paths))
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    Assert.Equal(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(paths[i]))), hasher.WaitForHash(i));
                }
            }
        }

        [Fact]
        public void TestWaitForHash__Missing()
        {
            string[] paths = { Path.Combine(dir, "a.cfg"), Path.Combine(dir, "missing.cfg"), Path.Combine(dir, "b.cfg") };
            File.WriteAllText(paths[0], "a");
            File.WriteAllText(paths[2], "b");

            using ConfigFileHasher hasher = new ConfigFileHasher(paths);
            Assert.Equal(Hash("a"), hasher.WaitForHash(0));
            Assert.Throws<FileNotFoundException>(() => hasher.WaitForHash(1));
        }

        [Fact]
        public void TestReadAhead()
        {
            // The workers stop a bounded number of files ahead of the caller, and Dispose wakes
            // them while they wait
            string[] paths = new string[300];
            for (int i = 0; i < paths.Length; i++)
            {
                paths[i] = Path.Combine(dir, i + ".cfg");
                File.WriteAllText(paths[i], i.ToString());
            }

            using (ConfigFileHasher hasher = new ConfigFileHasher(paths))
            {
                System.Threading.Thread.Sleep(300);
                Assert.InRange(hasher.ReadCount, 1, 65);
                for (int i = 0; i < 100; i++)
                    Assert.Equal(Hash(i.ToString()), hasher.WaitForHash(i));
                System.Threading.Thread.Sleep(300);
                Assert.InRange(hasher.ReadCount, 100, 165);
            }

            using (ConfigFileHasher hasher = new ConfigFileHasher(paths))
            {
                for (int i = 0; i < paths.Length; i++)
                    Assert.Equal(Hash(i.ToString()), hasher.WaitForHash(i));
            }
        }

        private static string Hash(string text)
        {
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        [Fact]
        public void TestWaitForHash__Empty()
        {
            using ConfigFileHasher hasher = new ConfigFileHasher(new string[0]);
        }
    }
}
