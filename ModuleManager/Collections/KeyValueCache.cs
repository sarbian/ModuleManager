using System;
using System.Collections.Generic;

namespace ModuleManager.Collections
{
    public class KeyValueCache<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> dict = new Dictionary<TKey, TValue>();
        private readonly object lockObject = new object();

        public TValue Fetch(TKey key, Func<TValue> createValue)
        {
            if (createValue == null) throw new ArgumentNullException(nameof(createValue));
            lock(lockObject)
            {
                if (dict.TryGetValue(key, out TValue value))
                {
                    return value;
                }
                else
                {
                    TValue newValue = createValue();
                    dict.Add(key, newValue);
                    return newValue;
                }
            }
        }

        // Takes the key as an argument so that callers can pass a delegate that captures nothing,
        // which the compiler caches, instead of allocating a closure on every call.
        public TValue FetchWithKey(TKey key, Func<TKey, TValue> createValue)
        {
            if (createValue == null) throw new ArgumentNullException(nameof(createValue));
            lock(lockObject)
            {
                if (dict.TryGetValue(key, out TValue value))
                {
                    return value;
                }
                else
                {
                    TValue newValue = createValue(key);
                    dict.Add(key, newValue);
                    return newValue;
                }
            }
        }
    }
}
