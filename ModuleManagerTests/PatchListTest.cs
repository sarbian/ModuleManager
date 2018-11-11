using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using NSubstitute;
using TestUtils;
using ModuleManager;
using ModuleManager.Patches;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;

namespace ModuleManagerTests
{
    public class PatchListTest
    {
        [Fact]
        public void TestConstructor__ModListNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new PatchList(null, new IPatch[0], Substitute.For<IPatchProgress>());
            });

            Assert.Equal("modList", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__PatchesNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new PatchList(new string[0], null, Substitute.For<IPatchProgress>());
            });

            Assert.Equal("patches", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__ProgressNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new PatchList(new string[0], new IPatch[0], null);
            });

            Assert.Equal("progress", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__UnknownMod()
        {
            IPatch patch = Substitute.For<IPatch>();
            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def", new ConfigNode("NODE"));
            patch.PassSpecifier.Returns(new BeforePassSpecifier("mod3", urlConfig));
            IPatchProgress progress = Substitute.For<IPatchProgress>();

            KeyNotFoundException ex = Assert.Throws<KeyNotFoundException>(delegate
            {
                new PatchList(new[] { "mod1", "mod2" }, new[] { patch }, progress);
            });

            Assert.Equal("Mod 'mod3' not found", ex.Message);

            progress.DidNotReceive().PatchAdded();
        }

        [Fact]
        public void TestConstructor__UnknownPassSpecifier()
        {
            IPatch patch = Substitute.For<IPatch>();
            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def", new ConfigNode("NODE"));
            IPassSpecifier passSpecifier = Substitute.For<IPassSpecifier>();
            passSpecifier.Descriptor.Returns(":SOMEPASS");
            patch.PassSpecifier.Returns(passSpecifier);
            IPatchProgress progress = Substitute.For<IPatchProgress>();

            NotImplementedException ex = Assert.Throws<NotImplementedException>(delegate
            {
                new PatchList(new string[0], new[] { patch }, progress);
            });

            Assert.Equal("Don't know what to do with pass specifier: :SOMEPASS", ex.Message);

            progress.DidNotReceive().PatchAdded();
        }

        [Fact]
        public void Test__Lifecycle()
        {
            IPatch[] patches = new IPatch[]
            {
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
            };

            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def", new ConfigNode("NODE"));

            patches[00].PassSpecifier.Returns(new FirstPassSpecifier());
            patches[01].PassSpecifier.Returns(new FirstPassSpecifier());
            patches[02].PassSpecifier.Returns(new LegacyPassSpecifier());
            patches[03].PassSpecifier.Returns(new LegacyPassSpecifier());
            patches[04].PassSpecifier.Returns(new BeforePassSpecifier("mod1", urlConfig));
            patches[05].PassSpecifier.Returns(new BeforePassSpecifier("MOD1", urlConfig));
            patches[06].PassSpecifier.Returns(new ForPassSpecifier("mod1", urlConfig));
            patches[07].PassSpecifier.Returns(new ForPassSpecifier("MOD1", urlConfig));
            patches[08].PassSpecifier.Returns(new AfterPassSpecifier("mod1", urlConfig));
            patches[09].PassSpecifier.Returns(new AfterPassSpecifier("MOD1", urlConfig));
            patches[10].PassSpecifier.Returns(new LastPassSpecifier("mod1"));
            patches[11].PassSpecifier.Returns(new LastPassSpecifier("MOD1"));
            patches[12].PassSpecifier.Returns(new BeforePassSpecifier("mod2", urlConfig));
            patches[13].PassSpecifier.Returns(new BeforePassSpecifier("MOD2", urlConfig));
            patches[14].PassSpecifier.Returns(new ForPassSpecifier("mod2", urlConfig));
            patches[15].PassSpecifier.Returns(new ForPassSpecifier("MOD2", urlConfig));
            patches[16].PassSpecifier.Returns(new AfterPassSpecifier("mod2", urlConfig));
            patches[17].PassSpecifier.Returns(new AfterPassSpecifier("MOD2", urlConfig));
            patches[18].PassSpecifier.Returns(new LastPassSpecifier("mod2"));
            patches[19].PassSpecifier.Returns(new LastPassSpecifier("MOD2"));
            patches[20].PassSpecifier.Returns(new FinalPassSpecifier());
            patches[21].PassSpecifier.Returns(new FinalPassSpecifier());

            IPatchProgress progress = Substitute.For<IPatchProgress>();

            PatchList patchList = new PatchList(new[] { "mod1", "mod2" }, patches, progress);

            IPass[] passes = patchList.ToArray();

            Assert.Equal(11, passes.Length);

            Assert.Equal(":FIRST", passes[0].Name);
            Assert.Equal(new[] { patches[0], patches[1] }, passes[0]);

            Assert.Equal(":LEGACY (default)", passes[1].Name);
            Assert.Equal(new[] { patches[2], patches[3] }, passes[1]);

            Assert.Equal(":BEFORE[MOD1]", passes[2].Name);
            Assert.Equal(new[] { patches[4], patches[5] }, passes[2]);

            Assert.Equal(":FOR[MOD1]", passes[3].Name);
            Assert.Equal(new[] { patches[6], patches[7] }, passes[3]);

            Assert.Equal(":AFTER[MOD1]", passes[4].Name);
            Assert.Equal(new[] { patches[8], patches[9] }, passes[4]);

            Assert.Equal(":BEFORE[MOD2]", passes[5].Name);
            Assert.Equal(new[] { patches[12], patches[13] }, passes[5]);

            Assert.Equal(":FOR[MOD2]", passes[6].Name);
            Assert.Equal(new[] { patches[14], patches[15] }, passes[6]);

            Assert.Equal(":AFTER[MOD2]", passes[7].Name);
            Assert.Equal(new[] { patches[16], patches[17] }, passes[7]);

            Assert.Equal(":LAST[MOD1]", passes[8].Name);
            Assert.Equal(new[] { patches[10], patches[11] }, passes[8]);

            Assert.Equal(":LAST[MOD2]", passes[9].Name);
            Assert.Equal(new[] { patches[18], patches[19] }, passes[9]);

            Assert.Equal(":FINAL", passes[10].Name);
            Assert.Equal(new[] { patches[20], patches[21] }, passes[10]);

            progress.Received(patches.Length).PatchAdded();
        }
    }
}
