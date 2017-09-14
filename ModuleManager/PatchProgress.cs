using System;
using System.Collections.Generic;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using NodeStack = ModuleManager.Collections.ImmutableStack<ConfigNode>;

namespace ModuleManager
{
    public class PatchProgress : IPatchProgress
    {
        public int TotalPatchCount { get; private set; } = 0;

        public int AppliedPatchCount { get; private set; } = 0;

        public int PatchedNodeCount { get; set; } = 0;

        public int ErrorCount { get; private set; } = 0;

        public int ExceptionCount { get; private set; } = 0;

        public int NeedsUnsatisfiedRootCount { get; private set; } = 0;

        public int NeedsUnsatisfiedCount { get; private set; } = 0;

        public Dictionary<String, int> ErrorFiles { get; } = new Dictionary<string, int>();

        private IBasicLogger logger;

        public float ProgressFraction
        {
            get
            {
                if (TotalPatchCount > 0)
                    return (AppliedPatchCount + NeedsUnsatisfiedRootCount) / (float)TotalPatchCount;
                return 0;
            }
        }

        public PatchProgress(IBasicLogger logger)
        {
            this.logger = logger;
        }

        public void PatchAdded()
        {
            TotalPatchCount += 1;
        }

        public void ApplyingUpdate(UrlDir.UrlConfig original, UrlDir.UrlConfig patch)
        {
            logger.Info($"Applying update {patch.SafeUrl()} to {original.SafeUrl()}");
            PatchedNodeCount += 1;
        }

        public void ApplyingCopy(UrlDir.UrlConfig original, UrlDir.UrlConfig patch)
        {
            logger.Info($"Applying copy {patch.SafeUrl()} to {original.SafeUrl()}");
            PatchedNodeCount += 1;
        }

        public void ApplyingDelete(UrlDir.UrlConfig original, UrlDir.UrlConfig patch)
        {
            logger.Info($"Applying delete {patch.SafeUrl()} to {original.SafeUrl()}");
            PatchedNodeCount += 1;
        }

        public void PatchApplied()
        {
            AppliedPatchCount += 1;
        }

        public void NeedsUnsatisfiedRoot(UrlDir.UrlConfig url)
        {
            logger.Info($"Deleting root node in file {url.parent.url} node: {url.type} as it can't satisfy its NEEDS");
            NeedsUnsatisfiedCount += 1;
            NeedsUnsatisfiedRootCount += 1;
        }

        public void NeedsUnsatisfiedNode(UrlDir.UrlConfig url, NodeStack path)
        {
            logger.Info($"Deleting node in file {url.parent.url} subnode: {path.GetPath()} as it can't satisfy its NEEDS");
            NeedsUnsatisfiedCount += 1;
        }

        public void NeedsUnsatisfiedValue(UrlDir.UrlConfig url, NodeStack path, string valName)
        {
            logger.Info($"Deleting value in file {url.parent.url} subnode: {path.GetPath()} value: {valName} as it can't satisfy its NEEDS");
            NeedsUnsatisfiedCount += 1;
        }

        public void Error(UrlDir.UrlConfig url, string message)
        {
            ErrorCount += 1;
            logger.Error(message);
            RecordErrorFile(url);
        }

        public void Exception(string message, Exception exception)
        {
            ExceptionCount += 1;
            logger.Exception(message, exception);
        }

        public void Exception(UrlDir.UrlConfig url, string message, Exception exception)
        {
            Exception(message, exception);
            RecordErrorFile(url);
        }

        private void RecordErrorFile(UrlDir.UrlConfig url)
        {
            string key = url.parent.url + "." + url.parent.fileExtension;
            if (key[0] == '/')
                key = key.Substring(1);

            if (ErrorFiles.ContainsKey(key))
                ErrorFiles[key] += 1;
            else
                ErrorFiles[key] = 1;
        }
    }
}
