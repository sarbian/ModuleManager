using System;
using Xunit;
using ModuleManager.Collections;

namespace ModuleManagerTests.Collections
{
    public class KeyValueCacheTest
    {
        [Fact]
        public void TestFetch__CreateValueNull()
        {
            KeyValueCache<object, object> cache = new KeyValueCache<object, object>();
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                cache.Fetch(new object(), null);
            });

            Assert.Equal("createValue", ex.ParamName);
        }

        [Fact]
        public void TestFetch__KeyNotPresent()
        {
            object key = new object();
            object value = new object();
            KeyValueCache<object, object> cache = new KeyValueCache<object, object>();

            object fetchedValue = cache.Fetch(key, () => value);

            Assert.Same(value, fetchedValue);
        }

        [Fact]
        public void TestFetch__KeyPresent()
        {
            object key = new object();
            object value = new object();
            KeyValueCache<object, object> cache = new KeyValueCache<object, object>();

            cache.Fetch(key, () => value);

            bool called2ndTime = false;
            object fetchedValue = cache.Fetch(key, delegate
            {
                called2ndTime = true;
                return null;
            });

            Assert.Same(value, fetchedValue);
            Assert.False(called2ndTime);
        }
    }
}
