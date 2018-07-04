using System;
using Xunit;
using NSubstitute;
using TestUtils;
using ModuleManager;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;

namespace ModuleManagerTests.Patches
{
    public class ForPassSpecifierTest
    {
        public readonly UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def", new ConfigNode("NODE"));
        public readonly INeedsChecker needsChecker = Substitute.For<INeedsChecker>();
        public readonly IPatchProgress progress = Substitute.For<IPatchProgress>();
        public readonly ForPassSpecifier passSpecifier;

        public ForPassSpecifierTest()
        {
            passSpecifier = new ForPassSpecifier("mod1", urlConfig);
        }

        [Fact]
        public void TestConstructor__ModNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new ForPassSpecifier(null, urlConfig);
            });

            Assert.Equal("mod", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__ModEmpty()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                new ForPassSpecifier("", urlConfig);
            });

            Assert.Equal("mod", ex.ParamName);
            Assert.Contains("can't be empty", ex.Message);
        }

        [Fact]
        public void TestConstructor__UrlConfigNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new ForPassSpecifier("mod1", null);
            });

            Assert.Equal("urlConfig", ex.ParamName);
        }

        [Fact]
        public void TestCheckNeeds__False()
        {
            needsChecker.CheckNeeds("mod1").Returns(false);
            Assert.False(passSpecifier.CheckNeeds(needsChecker, progress));

            progress.Received().NeedsUnsatisfiedFor(urlConfig);
        }

        [Fact]
        public void TestCheckNeeds__True()
        {
            needsChecker.CheckNeeds("mod1").Returns(true);
            Assert.True(passSpecifier.CheckNeeds(needsChecker, progress));

            progress.DidNotReceiveWithAnyArgs().NeedsUnsatisfiedFor(null);
        }

        [Fact]
        public void TestCheckNeeds__NeedsCheckerNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                passSpecifier.CheckNeeds(null, progress);
            });

            Assert.Equal("needsChecker", ex.ParamName);

            progress.DidNotReceiveWithAnyArgs().NeedsUnsatisfiedFor(null);
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
            Assert.Equal(":FOR[MOD1]", passSpecifier.Descriptor);
        }
    }
}
