using System;
using Xunit;
using TestUtils;
using ModuleManager;

namespace ModuleManagerTests
{
    public class PassTest
    {
        private UrlDir.UrlFile file;

        public PassTest()
        {
            file = UrlBuilder.CreateFile("abc/def.cfg");
        }

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

            Assert.Equal("can't be empty\r\nParameter name: name", ex.Message);
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
            Patch[] patches =
            {
                CreatePatch(Command.Edit, new ConfigNode()),
                CreatePatch(Command.Copy, new ConfigNode()),
                CreatePatch(Command.Delete, new ConfigNode()),
            };

            Pass pass = new Pass("blah")
            {
                patches[0],
                patches[1],
                patches[2],
            };

            Assert.Equal(patches, pass);
        }

        private Patch CreatePatch(Command command, ConfigNode node)
        {
            return new Patch(new UrlDir.UrlConfig(file, node), command, node);
        }
    }
}
