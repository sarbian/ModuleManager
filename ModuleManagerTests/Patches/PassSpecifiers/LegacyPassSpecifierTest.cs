using System;
using Xunit;
using NSubstitute;
using ModuleManager;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;

namespace ModuleManagerTests.Patches
{
    public class LegacyPassSpecifierTest
    {
        public readonly INeedsChecker needsChecker = Substitute.For<INeedsChecker>();
        public readonly IPatchProgress progress = Substitute.For<IPatchProgress>();
        private readonly LegacyPassSpecifier passSpecifier = new LegacyPassSpecifier();

        [Fact]
        public void TestCheckNeeds()
        {
            Assert.True(passSpecifier.CheckNeeds(needsChecker, progress));
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
            Assert.Equal(":LEGACY (default)", passSpecifier.Descriptor);
        }
    }
}
