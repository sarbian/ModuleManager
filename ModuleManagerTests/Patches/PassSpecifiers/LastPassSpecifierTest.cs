using System;
using Xunit;
using NSubstitute;
using ModuleManager;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;

namespace ModuleManagerTests.Patches
{
    public class LastPassSpecifierTest
    {
        public readonly INeedsChecker needsChecker = Substitute.For<INeedsChecker>();
        public readonly IPatchProgress progress = Substitute.For<IPatchProgress>();
        public readonly LastPassSpecifier passSpecifier;

        public LastPassSpecifierTest()
        {
            passSpecifier = new LastPassSpecifier("mod1");
        }

        [Fact]
        public void TestConstructor__ModNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new LastPassSpecifier(null);
            });

            Assert.Equal("mod", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__ModEmpty()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                new LastPassSpecifier("");
            });

            Assert.Equal("mod", ex.ParamName);
            Assert.Contains("can't be empty", ex.Message);
        }

        [Fact]
        public void TestCheckNeeds()
        {
            passSpecifier.CheckNeeds(needsChecker, progress);

            needsChecker.DidNotReceiveWithAnyArgs().CheckNeeds(null);
        }

        [Fact]
        public void TestDescriptor()
        {
            Assert.Equal(":LAST[MOD1]", passSpecifier.Descriptor);
        }
    }
}
