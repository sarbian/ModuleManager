using System;
using Xunit;
using NSubstitute;
using TestUtils;
using ModuleManager;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;

namespace ModuleManagerTests.Patches
{
    public class AfterPassSpecifierTest
    {
        public readonly UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def", new ConfigNode("NODE"));
        public readonly INeedsChecker needsChecker = Substitute.For<INeedsChecker>();
        public readonly IPatchProgress progress = Substitute.For<IPatchProgress>();
        public readonly AfterPassSpecifier passSpecifier;

        public AfterPassSpecifierTest()
        {
            passSpecifier = new AfterPassSpecifier("mod1", urlConfig);
        }

        [Fact]
        public void TestConstructor__ModNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new AfterPassSpecifier(null, urlConfig);
            });

            Assert.Equal("mod", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__ModEmpty()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                new AfterPassSpecifier("", urlConfig);
            });

            Assert.Equal("mod", ex.ParamName);
            Assert.Contains("can't be empty", ex.Message);
        }

        [Fact]
        public void TestConstructor__UrlConfigNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new AfterPassSpecifier("mod1", null);
            });

            Assert.Equal("urlConfig", ex.ParamName);
        }

        [Fact]
        public void TestCheckNeeds__False()
        {
            needsChecker.CheckNeeds("mod1").Returns(false);
            Assert.False(passSpecifier.CheckNeeds(needsChecker, progress));

            progress.Received().NeedsUnsatisfiedAfter(urlConfig);
        }

        [Fact]
        public void TestCheckNeeds__True()
        {
            needsChecker.CheckNeeds("mod1").Returns(true);
            Assert.True(passSpecifier.CheckNeeds(needsChecker, progress));

            progress.DidNotReceiveWithAnyArgs().NeedsUnsatisfiedAfter(null);
        }

        [Fact]
        public void TestCheckNeeds__NeedsCheckerNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                passSpecifier.CheckNeeds(null, progress);
            });

            Assert.Equal("needsChecker", ex.ParamName);

            progress.DidNotReceiveWithAnyArgs().NeedsUnsatisfiedAfter(null);
        }

        [Fact]
        public void TestCheckNeeds__ProgressNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                passSpecifier.CheckNeeds(needsChecker, null);
            });

            Assert.Equal("progress", ex.ParamName);
        }

        [Fact]
        public void TestDescriptor()
        {
            Assert.Equal(":AFTER[MOD1]", passSpecifier.Descriptor);
        }
    }
}
