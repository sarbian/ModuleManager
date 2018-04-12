using System;

namespace ModuleManager
{
    public class Patch
    {
        public readonly UrlDir.UrlConfig urlConfig;
        public readonly Command command;
        public readonly ConfigNode node;
        public readonly INodeMatcher nodeMatcher;

        public Patch(UrlDir.UrlConfig urlConfig, Command command, ConfigNode node)
        {
            if (command != Command.Edit && command != Command.Copy && command != Command.Delete)
                throw new ArgumentException($"Must be Edit, Copy, or Delete (got {command})", nameof(command));

            this.urlConfig = urlConfig ?? throw new ArgumentNullException(nameof(urlConfig));
            this.command = command;
            this.node = node ?? throw new ArgumentNullException(nameof(node));

            nodeMatcher = new NodeMatcher(node.name);
        }
    }
}
