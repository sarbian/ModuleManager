using System;

namespace ModuleManager.Patches
{
    public static class PatchCompiler
    {
        public static IPatch CompilePatch(UrlDir.UrlConfig urlConfig, Command command, string name)
        {
            if (urlConfig == null) throw new ArgumentNullException(nameof(urlConfig));
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (name == string.Empty) throw new ArgumentException("can't be empty", nameof(name));

            INodeMatcher nodeMatcher = new NodeMatcher(name);

            switch (command)
            {
                case Command.Edit:
                    return new EditPatch(urlConfig, nodeMatcher);

                case Command.Copy:
                    return new CopyPatch(urlConfig, nodeMatcher);

                case Command.Delete:
                    return new DeletePatch(urlConfig, nodeMatcher);

                default:
                    throw new ArgumentException("invalid command for a root node: " + command, nameof(command));
            }
        }
    }
}
