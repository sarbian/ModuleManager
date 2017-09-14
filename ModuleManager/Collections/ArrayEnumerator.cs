using System;
using System.Collections;
using System.Collections.Generic;

namespace ModuleManager.Collections
{
    public struct ArrayEnumerator<T> : IEnumerator<T>
    {
        private readonly T[] array;
        private int index;

        public ArrayEnumerator(T[] array)
        {
            this.array = array;
            index = -1;
        }

        public T Current => array[index];
        object IEnumerator.Current => Current;

        public void Dispose() { }

        public bool MoveNext()
        {
            index++;
            return index < array.Length;
        }

        public void Reset()
        {
            index = -1;
        }
    }
}
