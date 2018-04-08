using System;
using Xunit;
using ModuleManager;

namespace ModuleManagerTests
{
    public class OperatorParserTest
    {
        [Fact]
        public void TestParse__Null()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                OperatorParser.Parse(null, out string _);
            });

            Assert.Equal("name", ex.ParamName);
        }

        [Fact]
        public void TestParse__Empty()
        {
            Operator op = OperatorParser.Parse("", out string result);

            Assert.Equal(Operator.Assign, op);
            Assert.Equal("", result);
        }

        [Fact]
        public void TestParse__Assign()
        {
            Operator op = OperatorParser.Parse("some_stuff,1[2, ]", out string result);

            Assert.Equal(Operator.Assign, op);
            Assert.Equal("some_stuff,1[2, ]", result);
        }

        [Fact]
        public void TestParse__Add()
        {
            Operator op = OperatorParser.Parse("some_stuff,1[2, ]  +", out string result);

            Assert.Equal(Operator.Add, op);
            Assert.Equal("some_stuff,1[2, ]", result);
        }

        [Fact]
        public void TestParse__Subtract()
        {
            Operator op = OperatorParser.Parse("some_stuff,1[2, ]  -", out string result);

            Assert.Equal(Operator.Subtract, op);
            Assert.Equal("some_stuff,1[2, ]", result);
        }

        [Fact]
        public void TestParse__Multiply()
        {
            Operator op = OperatorParser.Parse("some_stuff,1[2, ]  *", out string result);

            Assert.Equal(Operator.Multiply, op);
            Assert.Equal("some_stuff,1[2, ]", result);
        }

        [Fact]
        public void TestParse__Divide()
        {
            Operator op = OperatorParser.Parse("some_stuff,1[2, ]  /", out string result);

            Assert.Equal(Operator.Divide, op);
            Assert.Equal("some_stuff,1[2, ]", result);
        }

        [Fact]
        public void TestParse__Exponentiate()
        {
            Operator op = OperatorParser.Parse("some_stuff,1[2, ]  !", out string result);

            Assert.Equal(Operator.Exponentiate, op);
            Assert.Equal("some_stuff,1[2, ]", result);
        }

        [Fact]
        public void TestParse__RegexReplace()
        {
            Operator op = OperatorParser.Parse("some_stuff,1[2, ]  ^", out string result);

            Assert.Equal(Operator.RegexReplace, op);
            Assert.Equal("some_stuff,1[2, ]", result);
        }
    }
}
