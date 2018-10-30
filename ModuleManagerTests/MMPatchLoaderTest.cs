using System;
using System.Linq;
using Xunit;
using NSubstitute;
using UnityEngine;
using TestUtils;
using ModuleManager;
using ModuleManager.Logging;
using ModuleManager.Progress;
using NodeStack = ModuleManager.Collections.ImmutableStack<ConfigNode>;

namespace ModuleManagerTests
{
    // This is not intended to fully test ModifyNode, however it is useful to include tests for bugfixes here before it is split up
    public class MMPatchLoaderTest
    {
        private readonly IBasicLogger logger = Substitute.For<IBasicLogger>();
        private readonly IPatchProgress progress = Substitute.For<IPatchProgress>();

        [Fact]
        public void TestModifyNode__IndexAllWithAssign()
        {
            ConfigNode c1 = new TestConfigNode("NODE")
            {
                { "foo", "bar1" },
                { "foo", "bar2" },
            };

            UrlDir.UrlConfig c2u = UrlBuilder.CreateConfig("abc/def", new TestConfigNode("@NODE")
            {
                { "@foo,*", "bar3" },
            });
            
            PatchContext context = new PatchContext(c2u, Enumerable.Empty<IProtoUrlConfig>(), logger, progress);

            ConfigNode c3 = MMPatchLoader.ModifyNode(new NodeStack(c1), c2u.config, context);

            EnsureNoErrors();

            AssertConfigNodesEqual(new TestConfigNode("NODE")
            {
                { "foo", "bar3" },
                { "foo", "bar3" },
            }, c3);
        }

        [Fact]
        public void TestModifyNode__MultiplyValue()
        {
            ConfigNode c1 = new TestConfigNode("NODE")
            {
                { "foo", "3" },
                { "foo", "5" },
            };

            UrlDir.UrlConfig c2u = UrlBuilder.CreateConfig("abc/def", new TestConfigNode("@NODE")
            {
                { "@foo *", "2" },
            });

            PatchContext context = new PatchContext(c2u, Enumerable.Empty<IProtoUrlConfig>(), logger, progress);

            ConfigNode c3 = MMPatchLoader.ModifyNode(new NodeStack(c1), c2u.config, context);

            EnsureNoErrors();

            AssertConfigNodesEqual(new TestConfigNode("NODE")
            {
                { "foo", "6" },
                { "foo", "5" },
            }, c3);
        }

        private void AssertConfigNodesEqual(ConfigNode expected, ConfigNode observed)
        {
            Assert.Equal(expected.ToString(), observed.ToString());
        }

        private void EnsureNoErrors()
        {
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            logger.DidNotReceive().Log(LogType.Warning, Arg.Any<string>());
            logger.DidNotReceive().Log(LogType.Error, Arg.Any<string>());
            logger.DidNotReceive().Log(LogType.Exception, Arg.Any<string>());
        }
    }
}
