using System;

namespace ModuleManager.Threading
{
    public class TaskStatus : ITaskStatus
    {
        private bool isRunning = true;
        private Exception exception = null;
        private object lockObject = new object();

        public bool IsRunning => isRunning;
        public Exception Exception => exception;

        public bool IsFinished
        {
            get
            {
                lock (lockObject)
                {
                    return !isRunning && exception == null;
                }
            }
        }

        public bool IsExitedWithError
        {
            get
            {
                lock (lockObject)
                {
                    return !isRunning && exception != null;
                }
            }
        }

        public void Finished()
        {
            lock (lockObject)
            {
                if (!isRunning) throw new InvalidOperationException("Task is not running");
                isRunning = false;
            }
        }

        public void Error(Exception exception)
        {
            lock(lockObject)
            {
                if (!isRunning) throw new InvalidOperationException("Task is not running");
                this.exception = exception ?? throw new ArgumentNullException(nameof(exception));
                isRunning = false;
            }
        }
    }
}
