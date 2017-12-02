using System;
using Xunit;
using ModuleManager.Threading;

namespace ModuleManagerTests.Threading
{
    public class BackgroundTaskTest
    {
        [Fact]
        public void Test__Start()
        {
            bool finish = false;
            void Run()
            {
                while (!finish) continue;
            }

            ITaskStatus status = BackgroundTask.Start(Run);

            Assert.True(status.IsRunning);
            Assert.False(status.IsFinished);
            Assert.False(status.IsExitedWithError);
            Assert.Null(status.Exception);

            finish = true;

            while (status.IsRunning) continue;

            Assert.False(status.IsRunning);
            Assert.True(status.IsFinished);
            Assert.False(status.IsExitedWithError);
            Assert.Null(status.Exception);
        }

        [Fact]
        public void Test__Start__Exception()
        {
            bool finish = false;
            Exception ex = new Exception();
            void Run()
            {
                while (!finish) continue;
                throw ex;
            }

            ITaskStatus status = BackgroundTask.Start(Run);

            Assert.True(status.IsRunning);
            Assert.False(status.IsFinished);
            Assert.False(status.IsExitedWithError);
            Assert.Null(status.Exception);

            finish = true;

            while (status.IsRunning) continue;

            Assert.False(status.IsRunning);
            Assert.False(status.IsFinished);
            Assert.True(status.IsExitedWithError);
            Assert.Same(ex, status.Exception);
        }

        [Fact]
        public void Test__Run__ArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(delegate
            {
                BackgroundTask.Start(null);
            });
        }
    }
}
