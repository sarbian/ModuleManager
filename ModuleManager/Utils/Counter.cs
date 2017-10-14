using System;

namespace ModuleManager.Utils
{
    public class Counter
    {
        public int Value { get; private set; } = 0;

        public void Increment()
        {
            Value++;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public static implicit operator int(Counter counter) => counter.Value;
    }
}
