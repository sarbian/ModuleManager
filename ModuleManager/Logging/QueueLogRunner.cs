using System;
using System.IO;
using System.Threading;

using ModuleManager.Collections;

namespace ModuleManager.Logging
{
    public class QueueLogRunner
    {
        private enum State
        {
            Initialized,
            Running,
            StopRequested,
            Stopped,
        }

        private volatile State state = State.Initialized;
        private readonly object stopLock = new object();
        private readonly IMessageQueue<ILogMessage> logQueue;
        private readonly long timeToWaitForLogsMs;

        public QueueLogRunner(IMessageQueue<ILogMessage> logQueue, long timeToWaitForLogsMs = 50)
        {
            this.logQueue = logQueue ?? throw new ArgumentNullException(nameof(logQueue));
            if (timeToWaitForLogsMs < 0) throw new ArgumentException("must be non-negative", nameof(timeToWaitForLogsMs));
            this.timeToWaitForLogsMs = timeToWaitForLogsMs;
        }

        public void RequestStop()
        {
            if (state == State.StopRequested || state == State.Stopped) return;
            if (state != State.Running) throw new InvalidOperationException($"Cannot request stop from {state} state");
            lock (stopLock)
            {
                state = State.StopRequested;
                // Wakes Run if it waits for more messages: it then writes the last ones at once
                Monitor.PulseAll(stopLock);
            }
        }

        public void Run(IBasicLogger logger)
        {
            if (state != State.Initialized) throw new InvalidOperationException($"Cannot run from {state} state");
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            state = State.Running;

            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

            while (state == State.Running)
            {
                stopwatch.Start();

                foreach (ILogMessage message in logQueue.TakeAll())
                {
                    logger.Log(message);
                }

                long timeRemaining = timeToWaitForLogsMs - stopwatch.ElapsedMilliseconds;
                if (timeRemaining > 0)
                {
                    lock (stopLock)
                    {
                        if (state == State.Running)
                            Monitor.Wait(stopLock, (int)timeRemaining);
                    }
                }

                stopwatch.Reset();
            }

            foreach (ILogMessage message in logQueue.TakeAll())
            {
                logger.Log(message);
            }

            state = State.Stopped;
        }
    }
}
