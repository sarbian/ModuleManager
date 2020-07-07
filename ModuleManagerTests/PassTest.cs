using System;
using System.Linq;
using Xunit;
using NSubstitute;
using ModuleManager;
using ModuleManager.Patches;

namespace ModuleManagerTests
{
    public class PassTest
    {
        [Fact]
        public void TestConstructor__NameNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new Pass(null);
            });

            Assert.Equal("name", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__NameEmpty()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                new Pass("");
            });

            Assert.Contains("can't be empty", ex.Message);
            Assert.Equal("name", ex.ParamName);
        }

        [Fact]
        public void TestName()
        {
            Pass pass = new Pass(":NOTINAMILLIONYEARS");

            Assert.Equal(":NOTINAMILLIONYEARS", pass.Name);
        }

        [Fact]
        public void Test__Add__Enumerator()
        {
            IPatch[] patches =
            {
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
                Substitute.For<IPatch>(),
            };

            Pass pass = new Pass("blah")
            {
                patches[0],
                patches[1],
                patches[2],
            };

            IPatch[] passPatches = pass.ToArray();
            Assert.Equal(patches.Length, passPatches.Length);

            for (int i = 0; i < patches.Length; i++)
            {
                Assert.Same(patches[i], passPatches[i]);
            }
        }
    }
}
