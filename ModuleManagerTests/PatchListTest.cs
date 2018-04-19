using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using NSubstitute;
using TestUtils;
using ModuleManager;
using ModuleManager.Patches;

namespace ModuleManagerTests
{
    public class PatchListTest
    {
        private UrlDir databaseRoot;
        private UrlDir.UrlFile file;
        private PatchList patchList;

        public PatchListTest()
        {
            databaseRoot = UrlBuilder.CreateRoot();
            file = UrlBuilder.CreateFile("abc/def.cfg", databaseRoot);

            patchList = new PatchList(new[] { "mod1", "mod2" });
        }

        [Fact]
        public void Test__Lifecycle()
        {
            IPatch patch01 = Substitute.For<IPatch>();
            IPatch patch02 = Substitute.For<IPatch>();
            IPatch patch03 = Substitute.For<IPatch>();
            IPatch patch04 = Substitute.For<IPatch>();
            IPatch patch05 = Substitute.For<IPatch>();
            IPatch patch06 = Substitute.For<IPatch>();
            IPatch patch07 = Substitute.For<IPatch>();
            IPatch patch08 = Substitute.For<IPatch>();
            IPatch patch09 = Substitute.For<IPatch>();
            IPatch patch10 = Substitute.For<IPatch>();
            IPatch patch11 = Substitute.For<IPatch>();
            IPatch patch12 = Substitute.For<IPatch>();
            IPatch patch13 = Substitute.For<IPatch>();
            IPatch patch14 = Substitute.For<IPatch>();
            IPatch patch15 = Substitute.For<IPatch>();
            IPatch patch16 = Substitute.For<IPatch>();
            IPatch patch17 = Substitute.For<IPatch>();
            IPatch patch18 = Substitute.For<IPatch>();

            patchList.AddFirstPatch(patch01);
            patchList.AddFirstPatch(patch02);
            patchList.AddLegacyPatch(patch03);
            patchList.AddLegacyPatch(patch04);
            patchList.AddBeforePatch("mod1", patch05);
            patchList.AddBeforePatch("MOD1", patch06);
            patchList.AddForPatch("mod1", patch07);
            patchList.AddForPatch("MOD1", patch08);
            patchList.AddAfterPatch("mod1", patch09);
            patchList.AddAfterPatch("MOD1", patch10);
            patchList.AddBeforePatch("mod2", patch11);
            patchList.AddBeforePatch("MOD2", patch12);
            patchList.AddForPatch("mod2", patch13);
            patchList.AddForPatch("MOD2", patch14);
            patchList.AddAfterPatch("mod2", patch15);
            patchList.AddAfterPatch("MOD2", patch16);
            patchList.AddFinalPatch(patch17);
            patchList.AddFinalPatch(patch18);

            IPass[] passes = patchList.ToArray();
            
            Assert.Equal(":FIRST", passes[0].Name);
            Assert.Equal(new[] { patch01, patch02 }, passes[0]);

            Assert.Equal(":LEGACY (default)", passes[1].Name);
            Assert.Equal(new[] { patch03, patch04 }, passes[1]);

            Assert.Equal(":BEFORE[MOD1]", passes[2].Name);
            Assert.Equal(new[] { patch05, patch06 }, passes[2]);

            Assert.Equal(":FOR[MOD1]", passes[3].Name);
            Assert.Equal(new[] { patch07, patch08 }, passes[3]);

            Assert.Equal(":AFTER[MOD1]", passes[4].Name);
            Assert.Equal(new[] { patch09, patch10 }, passes[4]);

            Assert.Equal(":BEFORE[MOD2]", passes[5].Name);
            Assert.Equal(new[] { patch11, patch12 }, passes[5]);

            Assert.Equal(":FOR[MOD2]", passes[6].Name);
            Assert.Equal(new[] { patch13, patch14 }, passes[6]);

            Assert.Equal(":AFTER[MOD2]", passes[7].Name);
            Assert.Equal(new[] { patch15, patch16 }, passes[7]);

            Assert.Equal(":FINAL", passes[8].Name);
            Assert.Equal(new[] { patch17, patch18 }, passes[8]);
        }

        [Fact]
        public void TestHasMod__True()
        {
            patchList = new PatchList(new[] { "mod1", "Mod2", "MOD3" });

            Assert.True(patchList.HasMod("mod1"));
            Assert.True(patchList.HasMod("Mod1"));
            Assert.True(patchList.HasMod("MOD1"));
            Assert.True(patchList.HasMod("mod2"));
            Assert.True(patchList.HasMod("Mod2"));
            Assert.True(patchList.HasMod("MOD2"));
            Assert.True(patchList.HasMod("mod3"));
            Assert.True(patchList.HasMod("Mod3"));
            Assert.True(patchList.HasMod("MOD3"));
        }

        [Fact]
        public void TestHasMod__False()
        {
            Assert.False(patchList.HasMod("mod3"));
            Assert.False(patchList.HasMod("Mod3"));
            Assert.False(patchList.HasMod("MOD3"));
        }

        [Fact]
        public void TestHasMod__Null()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                Assert.True(patchList.HasMod(null));
            });

            Assert.Equal("mod", ex.ParamName);
        }

        [Fact]
        public void TestHasMod__Blank()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                Assert.True(patchList.HasMod(""));
            });

            Assert.Equal("can't be empty\r\nParameter name: mod", ex.Message);
            Assert.Equal("mod", ex.ParamName);
        }

        [Fact]
        public void TestAddLegacyPatch__Null()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patchList.AddLegacyPatch(null);
            });

            Assert.Equal("patch", ex.ParamName);
        }

        [Fact]
        public void TestAddBeforePatch__ModNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patchList.AddBeforePatch(null, Substitute.For<IPatch>());
            });

            Assert.Equal("mod", ex.ParamName);
        }

        [Fact]
        public void TestAddBeforePatch__ModBlank()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                patchList.AddBeforePatch("", Substitute.For<IPatch>());
            });

            Assert.Equal("can't be empty\r\nParameter name: mod", ex.Message);
            Assert.Equal("mod", ex.ParamName);
        }

        [Fact]
        public void TestAddBeforePatch__PatchNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patchList.AddBeforePatch("mod1", null);
            });

            Assert.Equal("patch", ex.ParamName);
        }

        [Fact]
        public void TestAddBeforePatch__ModDoesNotExist()
        {
            KeyNotFoundException ex = Assert.Throws<KeyNotFoundException>(delegate
            {
                patchList.AddBeforePatch("mod3", Substitute.For<IPatch>());
            });

            Assert.Equal("Mod 'mod3' not found", ex.Message);
        }

        [Fact]
        public void TestAddForPatch__ModNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patchList.AddForPatch(null, Substitute.For<IPatch>());
            });

            Assert.Equal("mod", ex.ParamName);
        }

        [Fact]
        public void TestAddForPatch__ModBlank()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                patchList.AddForPatch("", Substitute.For<IPatch>());
            });

            Assert.Equal("can't be empty\r\nParameter name: mod", ex.Message);
            Assert.Equal("mod", ex.ParamName);
        }

        [Fact]
        public void TestAddForPatch__PatchNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patchList.AddForPatch("mod1", null);
            });

            Assert.Equal("patch", ex.ParamName);
        }

        [Fact]
        public void TestAddForPatch__ModDoesNotExist()
        {
            KeyNotFoundException ex = Assert.Throws<KeyNotFoundException>(delegate
            {
                patchList.AddForPatch("mod3", Substitute.For<IPatch>());
            });

            Assert.Equal("Mod 'mod3' not found", ex.Message);
        }

        [Fact]
        public void TestAddAfterPatch__ModNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patchList.AddAfterPatch(null, Substitute.For<IPatch>());
            });

            Assert.Equal("mod", ex.ParamName);
        }

        [Fact]
        public void TestAddAfterPatch__ModBlank()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                patchList.AddAfterPatch("", Substitute.For<IPatch>());
            });

            Assert.Equal("can't be empty\r\nParameter name: mod", ex.Message);
            Assert.Equal("mod", ex.ParamName);
        }

        [Fact]
        public void TestAddAfterPatch__PatchNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patchList.AddAfterPatch("mod1", null);
            });

            Assert.Equal("patch", ex.ParamName);
        }

        [Fact]
        public void TestAddAddafterPatch__ModDoesNotExist()
        {
            KeyNotFoundException ex = Assert.Throws<KeyNotFoundException>(delegate
            {
                patchList.AddAfterPatch("mod3", Substitute.For<IPatch>());
            });

            Assert.Equal("Mod 'mod3' not found", ex.Message);
        }

        [Fact]
        public void TestAddFinalPatch__Null()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patchList.AddFinalPatch(null);
            });

            Assert.Equal("patch", ex.ParamName);
        }
    }
}
