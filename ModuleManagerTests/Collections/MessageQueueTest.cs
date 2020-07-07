using System;
using Xunit;
using ModuleManager.Collections;

namespace ModuleManagerTests.Collections
{
    public class MessageQueueTest
    {
        private class TestClass { }

        private readonly MessageQueue<TestClass> queue = new MessageQueue<TestClass>();

        [Fact]
        public void Test__Empty()
        {
            Assert.Empty(queue);
        }

        [Fact]
        public void TestAdd()
        {
            TestClass o1 = new TestClass();
            TestClass o2 = new TestClass();
            TestClass o3 = new TestClass();

            queue.Add(o1);
            queue.Add(o2);
            queue.Add(o3);

            Assert.Equal(new[] { o1, o2, o3 }, queue);
        }

        [Fact]
        public void TestTakeAll()
        {
            TestClass o1 = new TestClass();
            TestClass o2 = new TestClass();
            TestClass o3 = new TestClass();
            TestClass o4 = new TestClass();

            queue.Add(o1);
            queue.Add(o2);
            queue.Add(o3);

            MessageQueue<TestClass> queue2 = Assert.IsType<MessageQueue<TestClass>>(queue.TakeAll());

            queue.Add(o4);

            Assert.Equal(new[] { o4 }, queue);
            Assert.Equal(new[] { o1, o2, o3 }, queue2);
        }
    }
}
