using System;
using System.Collections.Generic;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;

namespace ModuleManager.Patches
{
    public class InsertPatch : IPatch
    {
        public UrlDir.UrlConfig UrlConfig { get; }
        public string NodeType { get; }
        public IPassSpecifier PassSpecifier { get; }

        public InsertPatch(UrlDir.UrlConfig urlConfig, string nodeType, IPassSpecifier passSpecifier)
        {
            UrlConfig = urlConfig ?? throw new ArgumentNullException(nameof(urlConfig));
            NodeType = nodeType ?? throw new ArgumentNullException(nameof(nodeType));
            PassSpecifier = passSpecifier ?? throw new ArgumentNullException(nameof(passSpecifier));
        }

        public void Apply(LinkedList<IProtoUrlConfig> configs, IPatchProgress progress, IBasicLogger logger)
        {
            if (configs == null) throw new ArgumentNullException(nameof(configs));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            ConfigNode node = UrlConfig.config.DeepCopy();
            node.name = NodeType;
            configs.AddLast(new ProtoUrlConfig(UrlConfig.parent, node));
        }
    }
}
