using System;

namespace ModuleManager.Threading
{
    public class TaskStatus : ITaskStatus
    {
        private readonly object lockObject = new object();

        public bool IsRunning { get; private set; } = true;
        public Exception Exception { get; private set; } = null;

        public bool IsFinished
        {
            get
            {
                lock (lockObject)
                {
                    return !IsRunning && Exception == null;
                }
            }
        }

        public bool IsExitedWithError
        {
            get
            {
                lock (lockObject)
                {
                    return !IsRunning && Exception != null;
                }
            }
        }

        public void Finished()
        {
            lock (lockObject)
            {
                if (!IsRunning) throw new InvalidOperationException("Task is not running");
                IsRunning = false;
            }
        }

        public void Error(Exception exception)
        {
            lock(lockObject)
            {
                if (!IsRunning) throw new InvalidOperationException("Task is not running");
                this.Exception = exception ?? throw new ArgumentNullException(nameof(exception));
                IsRunning = false;
            }
        }
    }
}
