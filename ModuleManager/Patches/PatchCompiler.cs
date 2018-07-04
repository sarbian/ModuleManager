using System;

namespace ModuleManager.Patches
{
    public interface IPatchCompiler
    {
        IPatch CompilePatch(ProtoPatch protoPatch);
    }

    public class PatchCompiler : IPatchCompiler
    {
        public IPatch CompilePatch(ProtoPatch protoPatch)
        {
            if (protoPatch == null) throw new ArgumentNullException(nameof(protoPatch));

            INodeMatcher nodeMatcher = new NodeMatcher(protoPatch.nodeType, protoPatch.nodeName, protoPatch.has);

            switch (protoPatch.command)
            {
                case Command.Edit:
                    return new EditPatch(protoPatch.urlConfig, nodeMatcher, protoPatch.passSpecifier);

                case Command.Copy:
                    return new CopyPatch(protoPatch.urlConfig, nodeMatcher, protoPatch.passSpecifier);

                case Command.Delete:
                    return new DeletePatch(protoPatch.urlConfig, nodeMatcher, protoPatch.passSpecifier);

                default:
                    throw new ArgumentException("has an invalid command for a root node: " + protoPatch.command, nameof(protoPatch));
            }
        }
    }
}
