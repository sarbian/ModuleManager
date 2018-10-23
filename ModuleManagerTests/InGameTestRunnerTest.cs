using System;
using System.Linq;
using Xunit;
using NSubstitute;
using UnityEngine;
using TestUtils;
using ModuleManager;
using ModuleManager.Logging;

namespace ModuleManagerTests
{
    public class InGameTestRunnerTest
    {
        private readonly IBasicLogger logger;
        private readonly UrlDir databaseRoot;
        private readonly InGameTestRunner testRunner;

        public InGameTestRunnerTest()
        {
            logger = Substitute.For<IBasicLogger>();
            databaseRoot = UrlBuilder.CreateRoot();
            testRunner = new InGameTestRunner(logger);
        }

        [Fact]
        public void TestConstructor__LoggerNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new InGameTestRunner(null);
            });

            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void TestRunTestCases__DatabaseRootNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                testRunner.RunTestCases(null);
            });

            Assert.Equal("gameDatabaseRoot", ex.ParamName);
        }

        [Fact]
        public void TestRunTestCases__WrongNumberOfNodes()
        {
            UrlDir.UrlFile file1 = UrlBuilder.CreateFile("abc/blah1.cfg", databaseRoot);

            // Call CreateCopy otherwise XUnit sees that it's an IEnumerable and attempts to compare by enumeration
            ConfigNode testNode1 = new TestConfigNode("NODE1")
            {
                { "key1", "value1" },
            }.CreateCopy();

            ConfigNode testNode2 = new ConfigNode("NODE2");

            ConfigNode expectNode = new TestConfigNode("MMTEST_EXPECT")
            {
                new TestConfigNode("NODE1")
                {
                    { "key1", "value1" },
                },
            }.CreateCopy();

            UrlBuilder.CreateConfig(testNode1, file1);
            UrlBuilder.CreateConfig(testNode2, file1);
            UrlBuilder.CreateConfig(expectNode, file1);

            testRunner.RunTestCases(databaseRoot);

            Received.InOrder(delegate
            {
                logger.Log(LogType.Log, "Running tests...");
                logger.Log(LogType.Error, $"Test blah1 failed as expected number of nodes differs expected: 1 found: 2");
                logger.Log(LogType.Log, testNode1.ToString());
                logger.Log(LogType.Log, testNode2.ToString());
                logger.Log(LogType.Log, expectNode.ToString());
                logger.Log(LogType.Log, "tests complete.");
            });

            Assert.Equal(3, file1.configs.Count);
            Assert.Equal(testNode1, file1.configs[0].config);
            Assert.Equal(testNode2, file1.configs[1].config);
            Assert.Equal(expectNode, file1.configs[2].config);
        }

        [Fact]
        public void TestRunTestCases__AllPassing()
        {
            UrlDir.UrlFile file1 = UrlBuilder.CreateFile("abc/blah1.cfg", databaseRoot);
            UrlDir.UrlFile file2 = UrlBuilder.CreateFile("abc/blah2.cfg", databaseRoot);

            ConfigNode testNode1 = new TestConfigNode("NODE1")
            {
                { "key1", "value1" },
                { "key2", "value2" },
                new TestConfigNode("NODE2")
                {
                    { "key3", "value3" },
                },
            };

            ConfigNode testNode2 = new TestConfigNode("NODE3")
            {
                { "key4", "value4" },
            };

            ConfigNode testNode3 = new TestConfigNode("NODE4")
            {
                { "key5", "value5" },
            };

            UrlBuilder.CreateConfig(testNode1, file1);
            UrlBuilder.CreateConfig(testNode2, file1);
            UrlBuilder.CreateConfig(new TestConfigNode("MMTEST_EXPECT")
            {
                testNode1.CreateCopy(),
                testNode2.CreateCopy(),
            }, file1);

            UrlBuilder.CreateConfig(testNode3, file2);
            UrlBuilder.CreateConfig(new TestConfigNode("MMTEST_EXPECT")
            {
                testNode3.CreateCopy(),
            }, file2);

            testRunner.RunTestCases(databaseRoot);

            Received.InOrder(delegate
            {
                logger.Log(LogType.Log, "Running tests...");
                logger.Log(LogType.Log, "tests complete.");
            });

            logger.DidNotReceive().Log(LogType.Error, Arg.Any<string>());

            Assert.Empty(file1.configs);
            Assert.Empty(file2.configs);
        }

        [Fact]
        public void TestRunTestCases__Failure()
        {
            UrlDir.UrlFile file1 = UrlBuilder.CreateFile("abc/blah1.cfg", databaseRoot);

            ConfigNode testNode1 = new TestConfigNode("NODE1")
            {
                { "key1", "value1" },
                { "key2", "value2" },
                new TestConfigNode("NODE2")
                {
                    { "key3", "value3" },
                },
            };

            ConfigNode expectNode1 = new TestConfigNode("NODE1")
            {
                { "key1", "value1" },
                { "key2", "value2" },
                new TestConfigNode("NODE2")
                {
                    { "key4", "value3" },
                },
            };

            UrlBuilder.CreateConfig(testNode1, file1);
            UrlBuilder.CreateConfig(new TestConfigNode("MMTEST_EXPECT")
            {
                expectNode1,
            }, file1);

            testRunner.RunTestCases(databaseRoot);

            Received.InOrder(delegate
            {
                logger.Log(LogType.Log, "Running tests...");
                logger.Log(LogType.Error, $"Test blah1[0] failed as expected output and actual output differ.\nexpected:\n{expectNode1}\nActually got:\n{testNode1}");
                logger.Log(LogType.Log, "tests complete.");
            });


            Assert.Empty(file1.configs);
        }
    }
}
