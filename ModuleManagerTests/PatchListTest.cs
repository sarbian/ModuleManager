using System;
using System.Collections.Generic;
using Xunit;
using ModuleManager;

namespace ModuleManagerTests
{
    public class PatchListTest
    {
        [Fact]
        public void TestConstructor()
        {
            PatchList list = new PatchList(new string[0]);

            Assert.NotNull(list.firstPatches);
            Assert.NotNull(list.legacyPatches);
            Assert.NotNull(list.finalPatches);
            Assert.NotNull(list.modPasses);
        }

        [Fact]
        public void TestModPasses__HasMod()
        {
            PatchList list = new PatchList(new[] { "mod1", "Mod2", "MOD3" });

            PatchList.ModPassCollection collection = list.modPasses;

            Assert.True(collection.HasMod("mod1"));
            Assert.True(collection.HasMod("Mod1"));
            Assert.True(collection.HasMod("MOD1"));

            Assert.True(collection.HasMod("mod2"));
            Assert.True(collection.HasMod("Mod2"));
            Assert.True(collection.HasMod("MOD2"));

            Assert.True(collection.HasMod("mod3"));
            Assert.True(collection.HasMod("Mod3"));
            Assert.True(collection.HasMod("MOD3"));

            Assert.False(collection.HasMod("mod4"));
            Assert.False(collection.HasMod("Mod4"));
            Assert.False(collection.HasMod("MOD4"));
        }

        [Fact]
        public void TestModPasses__Accessor()
        {
            PatchList list = new PatchList(new[] { "mod1", "mod2" });

            PatchList.ModPass pass1 = list.modPasses["mod1"];
            Assert.NotNull(pass1);
            Assert.Equal("mod1", pass1.name);
            Assert.NotNull(pass1.beforePatches);
            Assert.Equal(0, pass1.beforePatches.Capacity);
            Assert.NotNull(pass1.forPatches);
            Assert.Equal(0, pass1.forPatches.Capacity);
            Assert.NotNull(pass1.afterPatches);
            Assert.Equal(0, pass1.afterPatches.Capacity);

            PatchList.ModPass pass2 = list.modPasses["mod2"];
            Assert.NotNull(pass2);
            Assert.Equal("mod2", pass2.name);
            Assert.NotNull(pass2.beforePatches);
            Assert.Equal(0, pass2.beforePatches.Capacity);
            Assert.NotNull(pass2.forPatches);
            Assert.Equal(0, pass2.forPatches.Capacity);
            Assert.NotNull(pass2.afterPatches);
            Assert.Equal(0, pass2.afterPatches.Capacity);

            Assert.Throws<KeyNotFoundException>(delegate
            {
                PatchList.ModPass mod3 = list.modPasses["mod3"];
            });
        }

        [Fact]
        public void TestModPasses__Enumeration()
        {
            PatchList list = new PatchList(new[] { "mod1", "mod2" });

            PatchList.ModPass[] passes = new PatchList.ModPass[] { list.modPasses["mod1"], list.modPasses["mod2"] };

            Assert.Equal(passes, list.modPasses);
        }
    }
}
