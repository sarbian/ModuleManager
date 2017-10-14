using System;
using Xunit;
using ModuleManager.Threading;

namespace ModuleManagerTests.Threading
{
    public class TaskStatusTest
    {
        [Fact]
        public void Test__Cosntructor()
        {
            TaskStatus status = new TaskStatus();

            Assert.True(status.IsRunning);
            Assert.False(status.IsFinished);
            Assert.False(status.IsExitedWithError);
            Assert.Null(status.Exception);
        }

        [Fact]
        public void TestFinished()
        {
            TaskStatus status = new TaskStatus();

            status.Finished();

            Assert.False(status.IsRunning);
            Assert.True(status.IsFinished);
            Assert.False(status.IsExitedWithError);
            Assert.Null(status.Exception);
        }

        [Fact]
        public void TestError()
        {
            TaskStatus status = new TaskStatus();
            Exception ex = new Exception();

            status.Error(ex);

            Assert.False(status.IsRunning);
            Assert.False(status.IsFinished);
            Assert.True(status.IsExitedWithError);
            Assert.Same(ex, status.Exception);
        }

        [Fact]
        public void TestFinished__AlreadyFinished()
        {
            TaskStatus status = new TaskStatus();

            status.Finished();

            Assert.Throws<InvalidOperationException>(delegate
            {
                status.Finished();
            });

            Assert.False(status.IsRunning);
            Assert.True(status.IsFinished);
            Assert.False(status.IsExitedWithError);
            Assert.Null(status.Exception);
        }

        [Fact]
        public void TestFinished__AlreadyErrored()
        {
            TaskStatus status = new TaskStatus();
            Exception ex = new Exception();

            status.Error(ex);

            Assert.Throws<InvalidOperationException>(delegate
            {
                status.Finished();
            });

            Assert.False(status.IsRunning);
            Assert.False(status.IsFinished);
            Assert.True(status.IsExitedWithError);
            Assert.Same(ex, status.Exception);
        }

        [Fact]
        public void TestError__AlreadyFinished()
        {
            TaskStatus status = new TaskStatus();

            status.Finished();

            Assert.Throws<InvalidOperationException>(delegate
            {
                status.Error(new Exception());
            });

            Assert.False(status.IsRunning);
            Assert.True(status.IsFinished);
            Assert.False(status.IsExitedWithError);
            Assert.Null(status.Exception);
        }

        [Fact]
        public void TestError__AlreadyErrored()
        {
            TaskStatus status = new TaskStatus();
            Exception ex = new Exception();

            status.Error(ex);

            Assert.Throws<InvalidOperationException>(delegate
            {
                status.Error(new Exception());
            });

            Assert.False(status.IsRunning);
            Assert.False(status.IsFinished);
            Assert.True(status.IsExitedWithError);
            Assert.Same(ex, status.Exception);
        }
    }
}
