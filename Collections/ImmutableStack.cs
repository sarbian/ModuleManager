using System;

namespace ModuleManager.Collections
{
    public class ImmutableStack<T>
    {
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
    }
}
