using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using ModuleManager;

namespace ModuleManagerTests
{
    public class CommandParserTest
    {
        [Fact]
        public void TestParse__Insert()
        {
            Assert.Equal(Command.Insert, CommandParser.Parse("PART", out string newName));
            Assert.Equal("PART", newName);
        }

        [Fact]
        public void TestParse__Delete()
        {
            Assert.Equal(Command.Delete, CommandParser.Parse("!PART", out string newName1));
            Assert.Equal("PART", newName1);
            Assert.Equal(Command.Delete, CommandParser.Parse("-PART", out string newName2));
            Assert.Equal("PART", newName2);
        }

        [Fact]
        public void TestParse__Edit()
        {
            Assert.Equal(Command.Edit, CommandParser.Parse("@PART", out string newName));
            Assert.Equal("PART", newName);
        }

        [Fact]
        public void TestParse__Replace()
        {
            Assert.Equal(Command.Replace, CommandParser.Parse("%PART", out string newName));
            Assert.Equal("PART", newName);
        }

        [Fact]
        public void TestParse__Copy()
        {
            Assert.Equal(Command.Copy, CommandParser.Parse("+PART", out string newName1));
            Assert.Equal("PART", newName1);
            Assert.Equal(Command.Copy, CommandParser.Parse("$PART", out string newName2));
            Assert.Equal("PART", newName2);
        }

        [Fact]
        public void TestParse__Rename()
        {
            Assert.Equal(Command.Rename, CommandParser.Parse("|PART", out string newName));
            Assert.Equal("PART", newName); ;
        }

        [Fact]
        public void TestParse__Paste()
        {
            Assert.Equal(Command.Paste, CommandParser.Parse("#PART", out string newName));
            Assert.Equal("PART", newName);
        }

        [Fact]
        public void TestParse__Special()
        {
            Assert.Equal(Command.Special, CommandParser.Parse("*PART", out string newName));
            Assert.Equal("PART", newName);
        }

        [Fact]
        public void TestParse__Special__Chained()
        {
            Assert.Equal(Command.Special, CommandParser.Parse("*@PART", out string newName));
            Assert.Equal("@PART", newName);
        }

        [Fact]
        public void TestParse__Create()
        {
            Assert.Equal(Command.Create, CommandParser.Parse("&PART", out string newName));
            Assert.Equal("PART", newName);
        }
    }
}
