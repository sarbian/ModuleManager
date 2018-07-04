using System;
using System.Collections;
using System.Collections.Generic;

namespace ModuleManager.Collections
{
    public struct ArrayEnumerator<T> : IEnumerator<T>
    {
        private readonly T[] array;
        private readonly int startIndex;
        private readonly int length;

        private int index;

        public ArrayEnumerator(params T[] array) : this(array, 0) { }

        public ArrayEnumerator(T[] array, int startIndex) : this(array, startIndex, (array?.Length ?? -1) - startIndex) { }

        public ArrayEnumerator(T[] array, int startIndex, int length)
        {
            this.array = array ?? throw new ArgumentNullException(nameof(array));

            if (startIndex < 0)
                throw new ArgumentException($"must be non-negative (got {startIndex})", nameof(startIndex));
            if (startIndex > array.Length)
                throw new ArgumentException(
                    $"must be less than or equal to array length (array length {array.Length}, startIndex {startIndex})",
                    nameof(startIndex)
                );
            if (length < 0)
                throw new ArgumentException($"must be non-negative (got {length})", nameof(length));
            if (startIndex + length > array.Length)
                throw new ArgumentException(
                    $"must fit within the string (array length {array.Length}, startIndex {startIndex}, length {length})",
                    nameof(length)
                );

            this.startIndex = startIndex;
            this.length = length;
            index = startIndex - 1;
        }

        public T Current => array[index];
        object IEnumerator.Current => Current;

        public void Dispose() { }

        public bool MoveNext()
        {
            index++;
            return index < startIndex + length;
        }

        public void Reset()
        {
            index = startIndex - 1;
        }
    }
}
