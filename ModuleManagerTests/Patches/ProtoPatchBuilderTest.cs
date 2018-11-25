using System;
using Xunit;
using NSubstitute;
using TestUtils;
using ModuleManager;
using ModuleManager.Collections;
using ModuleManager.Patches;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;
using ModuleManager.Tags;

namespace ModuleManagerTests.Patches
{
    public class ProtoPatchBuilderTest
    {
        private readonly IPatchProgress progress;
        private readonly ProtoPatchBuilder builder;
        private readonly UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def", new ConfigNode("NODE"));

        public ProtoPatchBuilderTest()
        {
            progress = Substitute.For<IPatchProgress>();
            builder = new ProtoPatchBuilder(progress);
        }

        [Fact]
        public void TestBuild__PrimaryValueNull()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<LegacyPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__PrimaryValue()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", "stuff", null));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Equal("stuff", protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<LegacyPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__Needs()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("NEEDS", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Equal("stuff", protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<LegacyPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__Needs__Case1()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("Needs", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Equal("stuff", protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<LegacyPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__Needs__Case2()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("needs", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Equal("stuff", protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<LegacyPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__Has()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("HAS", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Equal("stuff", protoPatch.has);
            Assert.IsType<LegacyPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__Has__Case1()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("Has", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Equal("stuff", protoPatch.has);
            Assert.IsType<LegacyPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__Has__Case2()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("has", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Equal("stuff", protoPatch.has);
            Assert.IsType<LegacyPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__First()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("FIRST", null, null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<FirstPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__First__Case1()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("First", null, null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<FirstPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__First__Case2()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("first", null, null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<FirstPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__Before()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("BEFORE", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            BeforePassSpecifier passSpecifier = Assert.IsType<BeforePassSpecifier>(protoPatch.passSpecifier);
            Assert.Equal("stuff", passSpecifier.mod);
            Assert.Same(urlConfig, passSpecifier.urlConfig);
        }

        [Fact]
        public void TestBuild__Before__Case1()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("Before", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            BeforePassSpecifier passSpecifier = Assert.IsType<BeforePassSpecifier>(protoPatch.passSpecifier);
            Assert.Equal("stuff", passSpecifier.mod);
            Assert.Same(urlConfig, passSpecifier.urlConfig);
        }

        [Fact]
        public void TestBuild__Before__Case2()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("before", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            BeforePassSpecifier passSpecifier = Assert.IsType<BeforePassSpecifier>(protoPatch.passSpecifier);
            Assert.Equal("stuff", passSpecifier.mod);
            Assert.Same(urlConfig, passSpecifier.urlConfig);
        }

        [Fact]
        public void TestBuild__For()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("FOR", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            ForPassSpecifier passSpecifier = Assert.IsType<ForPassSpecifier>(protoPatch.passSpecifier);
            Assert.Equal("stuff", passSpecifier.mod);
            Assert.Same(urlConfig, passSpecifier.urlConfig);
        }

        [Fact]
        public void TestBuild__For__Case1()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("For", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            ForPassSpecifier passSpecifier = Assert.IsType<ForPassSpecifier>(protoPatch.passSpecifier);
            Assert.Equal("stuff", passSpecifier.mod);
            Assert.Same(urlConfig, passSpecifier.urlConfig);
        }

        [Fact]
        public void TestBuild__For__Case2()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("for", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            ForPassSpecifier passSpecifier = Assert.IsType<ForPassSpecifier>(protoPatch.passSpecifier);
            Assert.Equal("stuff", passSpecifier.mod);
            Assert.Same(urlConfig, passSpecifier.urlConfig);
        }

        [Fact]
        public void TestBuild__After()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("AFTER", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            AfterPassSpecifier passSpecifier = Assert.IsType<AfterPassSpecifier>(protoPatch.passSpecifier);
            Assert.Equal("stuff", passSpecifier.mod);
            Assert.Same(urlConfig, passSpecifier.urlConfig);
        }

        [Fact]
        public void TestBuild__After__Case1()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("After", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            AfterPassSpecifier passSpecifier = Assert.IsType<AfterPassSpecifier>(protoPatch.passSpecifier);
            Assert.Equal("stuff", passSpecifier.mod);
            Assert.Same(urlConfig, passSpecifier.urlConfig);
        }

        [Fact]
        public void TestBuild__After__Case2()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("after", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            AfterPassSpecifier passSpecifier = Assert.IsType<AfterPassSpecifier>(protoPatch.passSpecifier);
            Assert.Equal("stuff", passSpecifier.mod);
            Assert.Same(urlConfig, passSpecifier.urlConfig);
        }

        [Fact]
        public void TestBuild__Last()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("LAST", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            LastPassSpecifier passSpecifier = Assert.IsType<LastPassSpecifier>(protoPatch.passSpecifier);
            Assert.Equal("stuff", passSpecifier.mod);
        }

        [Fact]
        public void TestBuild__Last__Case1()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("Last", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            LastPassSpecifier passSpecifier = Assert.IsType<LastPassSpecifier>(protoPatch.passSpecifier);
            Assert.Equal("stuff", passSpecifier.mod);
        }

        [Fact]
        public void TestBuild__Last__Case2()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("last", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            LastPassSpecifier passSpecifier = Assert.IsType<LastPassSpecifier>(protoPatch.passSpecifier);
            Assert.Equal("stuff", passSpecifier.mod);
        }

        [Fact]
        public void TestBuild__Final()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("FINAL", null, null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<FinalPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__Final__Case1()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("Final", null, null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<FinalPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__Final__Case2()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("final", null, null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<FinalPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__Insert__InsertPass()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("NEEDS", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Insert, tagList);

            EnsureNoErrors();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Insert, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Equal("stuff", protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<InsertPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__UrlConfigNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                builder.Build(null, Command.Edit, Substitute.For<ITagList>());
            });

            Assert.Equal("urlConfig", ex.ParamName);
        }

        [Fact]
        public void TestBuild__TagListNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                builder.Build(urlConfig, Command.Edit, null);
            });

            Assert.Equal("tagList", ex.ParamName);
        }

        [Fact]
        public void TestBuild__PrimaryValueEmpty()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", "", null));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            progress.Received().Warning(urlConfig, "empty brackets detected on patch name: abc/def/NODE");
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            EnsureNoExceptions();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<LegacyPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__NodeNameOnInsert()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", "blah", null));

            Assert.Null(builder.Build(urlConfig, Command.Insert, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, "name specifier detected on insert node (not a patch): abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__TrailerOnPrimaryTag()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", "stuff", "otherStuff"));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            progress.Received().Warning(urlConfig, "unrecognized trailer: 'otherStuff' on: abc/def/NODE");
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            EnsureNoExceptions();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Equal("stuff", protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<LegacyPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__TrailerOnSomeTag()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("NEEDS", "stuff", "morestuff")
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            progress.Received().Warning(urlConfig, "unrecognized trailer: 'morestuff' on: abc/def/NODE");
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            EnsureNoExceptions();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Equal("stuff", protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<LegacyPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__MoreThanOneNeeds()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("NEEDS", "stuff", null),
                new Tag("NEEDS", "otherStuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            progress.Received().Warning(urlConfig, "more than one :NEEDS tag detected, ignoring all but the first: abc/def/NODE");
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            EnsureNoExceptions();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Equal("stuff", protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<LegacyPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__NullNeeds()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("NEEDS", null, null)
            ));

            Assert.Null(builder.Build(urlConfig, Command.Copy, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, "empty :NEEDS tag detected: abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__EmptyNeeds()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("NEEDS", "", null)
            ));

            Assert.Null(builder.Build(urlConfig, Command.Copy, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, "empty :NEEDS tag detected: abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__MoreThanOneHas()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("HAS", "stuff", null),
                new Tag("HAS", "otherStuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            progress.Received().Warning(urlConfig, "more than one :HAS tag detected, ignoring all but the first: abc/def/NODE");
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            EnsureNoExceptions();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Equal("stuff", protoPatch.has);
            Assert.IsType<LegacyPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__NullHas()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("HAS", null, null)
            ));

            Assert.Null(builder.Build(urlConfig, Command.Copy, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, "empty :HAS tag detected: abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__EmptyHas()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("HAS", "", null)
            ));

            Assert.Null(builder.Build(urlConfig, Command.Copy, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, "empty :HAS tag detected: abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__HasOnInsert()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("HAS", "", null)
            ));

            Assert.Null(builder.Build(urlConfig, Command.Insert, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, ":HAS detected on insert node (not a patch): abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__BracketsOnFirst()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("FIRST", "", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            progress.Received().Warning(urlConfig, "value detected on :FIRST tag: abc/def/NODE");
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            EnsureNoExceptions();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<FirstPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__ValueOnFirst()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("FIRST", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            progress.Received().Warning(urlConfig, "value detected on :FIRST tag: abc/def/NODE");
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            EnsureNoExceptions();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<FirstPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__MoreThanOnePass__First()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("FINAL", null, null),
                new Tag("FIRST", null, null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            progress.Received().Warning(urlConfig, "more than one pass specifier detected, ignoring all but the first: abc/def/NODE");
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            EnsureNoExceptions();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<FinalPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__PassSpecifierOnInsert__First()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("FIRST", null, null)
            ));

            Assert.Null(builder.Build(urlConfig, Command.Insert, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, "pass specifier detected on insert node (not a patch): abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__NullBefore()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("BEFORE", null, null)
            ));

            Assert.Null(builder.Build(urlConfig, Command.Copy, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, "empty :BEFORE tag detected: abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__EmptyBefore()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("BEFORE", "", null)
            ));

            Assert.Null(builder.Build(urlConfig, Command.Copy, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, "empty :BEFORE tag detected: abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__MoreThanOnePass__Before()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("FIRST", null, null),
                new Tag("BEFORE", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            progress.Received().Warning(urlConfig, "more than one pass specifier detected, ignoring all but the first: abc/def/NODE");
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            EnsureNoExceptions();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<FirstPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__PassSpecifierOnInsert__Before()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("BEFORE", "mod1", null)
            ));

            Assert.Null(builder.Build(urlConfig, Command.Insert, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, "pass specifier detected on insert node (not a patch): abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__NullFor()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("FOR", null, null)
            ));

            Assert.Null(builder.Build(urlConfig, Command.Copy, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, "empty :FOR tag detected: abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__EmptyFor()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("FOR", "", null)
            ));

            Assert.Null(builder.Build(urlConfig, Command.Copy, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, "empty :FOR tag detected: abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__MoreThanOnePass__For()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("FIRST", null, null),
                new Tag("FOR", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            progress.Received().Warning(urlConfig, "more than one pass specifier detected, ignoring all but the first: abc/def/NODE");
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            EnsureNoExceptions();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<FirstPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__PassSpecifierOnInsert__For()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("FOR", "mod1", null)
            ));

            Assert.Null(builder.Build(urlConfig, Command.Insert, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, "pass specifier detected on insert node (not a patch): abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__NullAfter()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("AFTER", null, null)
            ));

            Assert.Null(builder.Build(urlConfig, Command.Copy, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, "empty :AFTER tag detected: abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__EmptyAfter()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("AFTER", "", null)
            ));

            Assert.Null(builder.Build(urlConfig, Command.Copy, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, "empty :AFTER tag detected: abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__MoreThanOnePass__After()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("FIRST", null, null),
                new Tag("AFTER", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            progress.Received().Warning(urlConfig, "more than one pass specifier detected, ignoring all but the first: abc/def/NODE");
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            EnsureNoExceptions();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<FirstPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__PassSpecifierOnInsert__After()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("AFTER", "mod1", null)
            ));

            Assert.Null(builder.Build(urlConfig, Command.Insert, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, "pass specifier detected on insert node (not a patch): abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__NullLast()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("LAST", null, null)
            ));

            Assert.Null(builder.Build(urlConfig, Command.Copy, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, "empty :LAST tag detected: abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__EmptyLast()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("LAST", "", null)
            ));

            Assert.Null(builder.Build(urlConfig, Command.Copy, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, "empty :LAST tag detected: abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__MoreThanOnePass__Last()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("FIRST", null, null),
                new Tag("LAST", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            progress.Received().Warning(urlConfig, "more than one pass specifier detected, ignoring all but the first: abc/def/NODE");
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            EnsureNoExceptions();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<FirstPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__PassSpecifierOnInsert__Last()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("LAST", "mod1", null)
            ));

            Assert.Null(builder.Build(urlConfig, Command.Insert, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, "pass specifier detected on insert node (not a patch): abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__BracketsOnFinal()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("FINAL", "", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            progress.Received().Warning(urlConfig, "value detected on :FINAL tag: abc/def/NODE");
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            EnsureNoExceptions();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<FinalPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__ValueOnFinal()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("FINAL", "stuff", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            progress.Received().Warning(urlConfig, "value detected on :FINAL tag: abc/def/NODE");
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            EnsureNoExceptions();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<FinalPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__MoreThanOnePass__Final()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("FIRST", null, null),
                new Tag("FINAL", null, null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            progress.Received().Warning(urlConfig, "more than one pass specifier detected, ignoring all but the first: abc/def/NODE");
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            EnsureNoExceptions();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<FirstPassSpecifier>(protoPatch.passSpecifier);
        }

        [Fact]
        public void TestBuild__PassSpecifierOnInsert__Final()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("FINAL", null, null)
            ));

            Assert.Null(builder.Build(urlConfig, Command.Insert, tagList));

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.Received().Error(urlConfig, "pass specifier detected on insert node (not a patch): abc/def/NODE");
            EnsureNoExceptions();
        }

        [Fact]
        public void TestBuild__UnrecognizedTag()
        {
            ITagList tagList = Substitute.For<ITagList>();
            tagList.PrimaryTag.Returns(new Tag("NODE", null, null));
            tagList.GetEnumerator().Returns(new ArrayEnumerator<Tag>(
                new Tag("SOMESTUFF", "blah", null)
            ));

            ProtoPatch protoPatch = builder.Build(urlConfig, Command.Copy, tagList);

            progress.Received().Warning(urlConfig, "unrecognized tag: 'SOMESTUFF' on: abc/def/NODE");
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            EnsureNoExceptions();

            Assert.Same(urlConfig, protoPatch.urlConfig);
            Assert.Equal(Command.Copy, protoPatch.command);
            Assert.Equal("NODE", protoPatch.nodeType);
            Assert.Null(protoPatch.nodeName);
            Assert.Null(protoPatch.needs);
            Assert.Null(protoPatch.has);
            Assert.IsType<LegacyPassSpecifier>(protoPatch.passSpecifier);
        }

        private void EnsureNoErrors()
        {
            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            EnsureNoExceptions();
        }

        private void EnsureNoExceptions()
        {
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);
        }
    }
}
