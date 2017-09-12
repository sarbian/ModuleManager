using System;
using System.Linq;
using Xunit;

using ModuleManager.Collections;

namespace ModuleManagerTests.Collections
{
    public class ImmutableStackTest
    {
        [Fact]
        public void TestValue()
        {
            object obj = new object();
            ImmutableStack<object> stack = new ImmutableStack<object>(obj);

            Assert.Same(obj, stack.value);
        }

        [Fact]
        public void TestIsRoot()
        {
            ImmutableStack<object> stack1 = new ImmutableStack<object>(new object());
            ImmutableStack<object> stack2 = stack1.Push(new object());

            Assert.True(stack1.IsRoot);
            Assert.False(stack2.IsRoot);
        }

        [Fact]
        public void TestRoot()
        {
            ImmutableStack<object> stack1 = new ImmutableStack<object>(new object());
            ImmutableStack<object> stack2 = stack1.Push(new object());
            ImmutableStack<object> stack3 = stack2.Push(new object());

            Assert.Same(stack1, stack1.Root);
            Assert.Same(stack1, stack2.Root);
            Assert.Same(stack1, stack3.Root);
        }

        [Fact]
        public void TestDepth()
        {
            ImmutableStack<object> stack1 = new ImmutableStack<object>(new object());
            ImmutableStack<object> stack2 = stack1.Push(new object());
            ImmutableStack<object> stack3 = stack2.Push(new object());

            Assert.Equal(1, stack1.Depth);
            Assert.Equal(2, stack2.Depth);
            Assert.Equal(3, stack3.Depth);
        }

        [Fact]
        public void TestPush()
        {
            object obj1 = new object();
            object obj2 = new object();
            object obj3 = new object();
            ImmutableStack<object> stack1 = new ImmutableStack<object>(obj1);
            ImmutableStack<object> stack2 = stack1.Push(obj2);
            ImmutableStack<object> stack3 = stack2.Push(obj3);

            Assert.Same(stack2, stack3.parent);
            Assert.Same(stack1, stack2.parent);

            Assert.Same(obj1, stack1.value);
            Assert.Same(obj2, stack2.value);
            Assert.Same(obj3, stack3.value);
        }

        [Fact]
        public void TestPop()
        {
            object obj1 = new object();
            object obj2 = new object();
            object obj3 = new object();
            ImmutableStack<object> stack = new ImmutableStack<object>(obj1).Push(obj2).Push(obj3);

            Assert.Same(obj1, stack.Pop().Pop().value);
            Assert.Same(obj2, stack.Pop().value);
            Assert.Same(obj3, stack.value);
        }

        [Fact]
        public void TestPop__Root()
        {
            ImmutableStack<object> stack = new ImmutableStack<object>(new object());

            Assert.Throws<InvalidOperationException>(delegate
            {
                stack.Pop();
            });
        }

        [Fact]
        public void TestReplaceValue()
        {
            object obj1 = new object();
            object obj2 = new object();
            object obj3 = new object();
            ImmutableStack<object> stack1 = new ImmutableStack<object>(obj1);
            ImmutableStack<object> stack2 = stack1.Push(obj2);

            ImmutableStack<object> stack3 = stack2.ReplaceValue(obj3);

            Assert.Same(obj3, stack3.value);
            Assert.Same(stack1, stack3.parent);
        }

        [Fact]
        public void TestReplaceValue__Root()
        {
            object obj1 = new object();
            object obj2 = new object();
            ImmutableStack<object> stack1 = new ImmutableStack<object>(obj1);

            ImmutableStack<object> stack2 = stack1.ReplaceValue(obj2);

            Assert.Same(obj2, stack2.value);
            Assert.Null(stack2.parent);
        }

        [Fact]
        public void TestEnumerator()
        {
            object obj1 = new object();
            object obj2 = new object();
            object obj3 = new object();
            ImmutableStack<object> stack = new ImmutableStack<object>(obj1).Push(obj2).Push(obj3);

            object[] objs = stack.ToArray();

            Assert.Equal(3, objs.Length);
            Assert.Same(obj3, objs[0]);
            Assert.Same(obj2, objs[1]);
            Assert.Same(obj1, objs[2]);
        }
    }
}
