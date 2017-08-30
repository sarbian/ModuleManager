using System;
using System.Collections;
using System.Collections.Generic;

namespace ModuleManager.Collections
{
    public class ImmutableStack<T> : IEnumerable<T>
    {
        public struct Enumerator : IEnumerator<T>
        {
            private ImmutableStack<T> head;
            private ImmutableStack<T> currentStack;

            public Enumerator(ImmutableStack<T> stack)
            {
                head = stack;
                currentStack = null;
            }

            public T Current => currentStack.value;
            object IEnumerator.Current => Current;

            public void Dispose() { }

            public bool MoveNext()
            {
                if (currentStack == null)
                {
                    currentStack = head;
                    return true;
                }
                else if (!currentStack.IsRoot)
                {
                    currentStack = currentStack.parent;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public void Reset() => currentStack = null;
        }

        public readonly T value;
        public readonly ImmutableStack<T> parent;

        public ImmutableStack(T value)
        {
            this.value = value;
        }

        private ImmutableStack(T value, ImmutableStack<T> parent)
        {
            this.value = value;
            this.parent = parent;
        }

        public bool IsRoot => parent == null;
        public ImmutableStack<T> Root => IsRoot? this : parent.Root;

        public int Depth => IsRoot ? 1 : parent.Depth + 1;

        public ImmutableStack<T> Push(T newValue)
        {
            return new ImmutableStack<T>(newValue, this);
        }

        public ImmutableStack<T> Pop()
        {
            if (IsRoot) throw new InvalidOperationException("Cannot pop from the root of a stack");
            return parent;
        }

        public ImmutableStack<T> ReplaceValue(T newValue) => new ImmutableStack<T>(newValue, parent);

        public Enumerator GetEnumerator() => new Enumerator(this);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
