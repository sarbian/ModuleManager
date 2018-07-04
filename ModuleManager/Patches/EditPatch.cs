using System;
using NodeStack = ModuleManager.Collections.ImmutableStack<ConfigNode>;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;

namespace ModuleManager.Patches
{
    public class EditPatch : IPatch
    {
        private readonly bool loop;

        public UrlDir.UrlConfig UrlConfig { get; }
        public INodeMatcher NodeMatcher { get; }
        public IPassSpecifier PassSpecifier { get; }

        public EditPatch(UrlDir.UrlConfig urlConfig, INodeMatcher nodeMatcher, IPassSpecifier passSpecifier)
        {
            UrlConfig = urlConfig ?? throw new ArgumentNullException(nameof(urlConfig));
            NodeMatcher = nodeMatcher ?? throw new ArgumentNullException(nameof(nodeMatcher));
            PassSpecifier = passSpecifier ?? throw new ArgumentNullException(nameof(passSpecifier));

            loop = urlConfig.config.HasNode("MM_PATCH_LOOP");
        }

        public void Apply(UrlDir.UrlFile file, IPatchProgress progress, IBasicLogger logger)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            PatchContext context = new PatchContext(UrlConfig, file.root, logger, progress);
            for (int i = 0; i < file.configs.Count; i++)
            {
                UrlDir.UrlConfig urlConfig = file.configs[i];
                try
                {
                    if (!NodeMatcher.IsMatch(urlConfig.config)) continue;
                    if (loop) logger.Info($"Looping on {UrlConfig.SafeUrl()} to {urlConfig.SafeUrl()}");

                    do
                    {
                        progress.ApplyingUpdate(urlConfig, UrlConfig);
                        file.configs[i] = urlConfig = new UrlDir.UrlConfig(file, MMPatchLoader.ModifyNode(new NodeStack(urlConfig.config), UrlConfig.config, context));
                    } while (loop && NodeMatcher.IsMatch(urlConfig.config));

                    if (loop) file.configs[i].config.RemoveNodes("MM_PATCH_LOOP");
                }
                catch (Exception ex)
                {
                    progress.Exception(UrlConfig, $"Exception while applying update {UrlConfig.SafeUrl()} to {urlConfig.SafeUrl()}", ex);
                }
            }
        }
    }
}
