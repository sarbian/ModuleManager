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

            return protoPatch.command switch
            {
                Command.Insert => new InsertPatch(protoPatch.urlConfig, protoPatch.nodeType, protoPatch.passSpecifier),
                Command.Edit => new EditPatch(protoPatch.urlConfig, new NodeMatcher(protoPatch.nodeType, protoPatch.nodeName, protoPatch.has), protoPatch.passSpecifier),
                Command.Copy => new CopyPatch(protoPatch.urlConfig, new NodeMatcher(protoPatch.nodeType, protoPatch.nodeName, protoPatch.has), protoPatch.passSpecifier),
                Command.Delete => new DeletePatch(protoPatch.urlConfig, new NodeMatcher(protoPatch.nodeType, protoPatch.nodeName, protoPatch.has), protoPatch.passSpecifier),
                _ => throw new ArgumentException("has an invalid command for a root node: " + protoPatch.command, nameof(protoPatch)),
            };
        }
    }
}
