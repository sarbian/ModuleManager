using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModuleManager.Utils
{
    public class Counter
    {
        public int Value { get; private set; } = 0;

        public void Increment()
        {
            Value++;
        }

        public static implicit operator int(Counter counter) => counter.Value;
    }
}
