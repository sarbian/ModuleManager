using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Threading;

namespace ModuleManager
{
    // Computes each file's SHA256 on worker threads, while the caller uses them in order.
    // WaitForHash(i) gives file i's hash once ready, or throws what reading it threw.
    //
    // The workers stay at most ReadAhead files ahead of the caller. If a worker thread dies (an abort), the file it was on is marked as failed
    // with that exception; if every worker is gone, the caller reads the files it waits for itself.
    public sealed class ConfigFileHasher : IDisposable
    {
        private const int ReadAhead = 64;

        private readonly string[] paths;
        private readonly string[] hashes;
        private readonly Exception[] errors;
        private readonly bool[] ready;
        private readonly Thread[] workers;
        private readonly object lockObject = new object();
        private int next = -1;
        private int waitingFor;
        private int liveWorkers;
        private bool stopping;
        private int readCount;

        // How many files the workers have read so far
        public int ReadCount
        {
            get { lock (lockObject) return readCount; }
        }

        public ConfigFileHasher(string[] paths)
        {
            this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
            hashes = new string[paths.Length];
            errors = new Exception[paths.Length];
            ready = new bool[paths.Length];

            int count = Math.Max(1, Math.Min(Environment.ProcessorCount - 1, 8));
            if (count > paths.Length) count = Math.Max(1, paths.Length);
            workers = new Thread[count];
            liveWorkers = count;
            for (int i = 0; i < count; i++)
            {
                workers[i] = new Thread(Work) { IsBackground = true, Name = "ModuleManager file hasher " + i };
                workers[i].Start();
            }
        }

        public string WaitForHash(int i)
        {
            bool readHere = false;
            lock (lockObject)
            {
                waitingFor = i;
                Monitor.PulseAll(lockObject);
                while (!ready[i])
                {
                    if (liveWorkers == 0)
                    {
                        // Nobody is left to read it: claim it if no worker has
                        if (i > next) next = i;
                        readHere = true;
                        break;
                    }
                    Monitor.Wait(lockObject);
                }
            }
            if (readHere) Read(i, null);

            if (errors[i] != null)
                ExceptionDispatchInfo.Capture(errors[i]).Throw();
            return hashes[i];
        }

        private void Read(int i, SHA256 sha)
        {
            try
            {
                using FileStream stream = new FileStream(paths[i], FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
                if (sha == null)
                {
                    using SHA256 own = SHA256.Create();
                    hashes[i] = BitConverter.ToString(own.ComputeHash(stream));
                }
                else
                {
                    hashes[i] = BitConverter.ToString(sha.ComputeHash(stream));
                }
            }
            catch (Exception e)
            {
                errors[i] = e;
            }
        }

        private void Work()
        {
            int current = -1;
            try
            {
                using SHA256 sha = SHA256.Create();
                while (true)
                {
                    lock (lockObject)
                    {
                        while (!stopping && next + 1 < paths.Length && next + 1 > waitingFor + ReadAhead)
                            Monitor.Wait(lockObject);
                        if (stopping || next + 1 >= paths.Length) return;
                        current = ++next;
                    }

                    Read(current, sha);

                    lock (lockObject)
                    {
                        ready[current] = true;
                        readCount++;
                        current = -1;
                        Monitor.PulseAll(lockObject);
                    }
                }
            }
            catch (Exception e)
            {
                lock (lockObject)
                {
                    if (current >= 0 && !ready[current])
                    {
                        errors[current] = e;
                        ready[current] = true;
                    }
                }
                // Not rethrown: a worker that cannot start (SHA256.Create) leaves its files to
                // the others, or to the caller, which then meets the same error itself
            }
            finally
            {
                lock (lockObject)
                {
                    // An abort that hit between two lines above still leaves no file half done
                    if (current >= 0 && !ready[current])
                    {
                        errors[current] = errors[current] ?? new ThreadInterruptedException("ModuleManager file hasher stopped while reading " + paths[current]);
                        ready[current] = true;
                    }
                    liveWorkers--;
                    Monitor.PulseAll(lockObject);
                }
            }
        }

        public void Dispose()
        {
            lock (lockObject)
            {
                stopping = true;
                Monitor.PulseAll(lockObject);
            }
            foreach (Thread worker in workers)
                worker.Join();
        }
    }
}
