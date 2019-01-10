using System;
using System.IO;

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

        private State state = State.Initialized;
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
            state = State.StopRequested;
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
                    message.LogTo(logger);
                }

                long timeRemaining = timeToWaitForLogsMs - stopwatch.ElapsedMilliseconds;
                if (timeRemaining > 0)
                    System.Threading.Thread.Sleep((int)timeRemaining);

                stopwatch.Reset();
            }

            foreach (ILogMessage message in logQueue.TakeAll())
            {
                message.LogTo(logger);
            }

            state = State.Stopped;
        }
    }
}
