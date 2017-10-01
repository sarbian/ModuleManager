using System;
using System.Collections.Generic;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using NodeStack = ModuleManager.Collections.ImmutableStack<ConfigNode>;

namespace ModuleManager
{
    public class PatchProgress : IPatchProgress
    {
        private class ProgressTracker
        {
            public int totalPatchCount = 0;
            public int appliedPatchCount = 0;
            public int patchedNodeCount = 0;
            public int errorCount = 0;
            public int exceptionCount = 0;
            public int needsUnsatisfiedRootCount = 0;
            public int needsUnsatisfiedCount = 0;
            
            public Dictionary<String, int> ErrorFiles { get; } = new Dictionary<string, int>();
        }

        public int TotalPatchCount => progressTracker.totalPatchCount;
        public int AppliedPatchCount => progressTracker.appliedPatchCount;
        public int PatchedNodeCount => progressTracker.patchedNodeCount;
        public int ErrorCount => progressTracker.errorCount;
        public int ExceptionCount => progressTracker.exceptionCount;
        public int NeedsUnsatisfiedRootCount => progressTracker.needsUnsatisfiedRootCount;
        public int NeedsUnsatisfiedCount => progressTracker.needsUnsatisfiedCount;
        public Dictionary<String, int> ErrorFiles => progressTracker.ErrorFiles;

        private IBasicLogger logger;
        private ProgressTracker progressTracker;

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
            progressTracker = new ProgressTracker();
        }

        public void PatchAdded()
        {
            progressTracker.totalPatchCount += 1;
        }

        public void ApplyingUpdate(UrlDir.UrlConfig original, UrlDir.UrlConfig patch)
        {
            logger.Info($"Applying update {patch.SafeUrl()} to {original.SafeUrl()}");
            progressTracker.patchedNodeCount += 1;
        }

        public void ApplyingCopy(UrlDir.UrlConfig original, UrlDir.UrlConfig patch)
        {
            logger.Info($"Applying copy {patch.SafeUrl()} to {original.SafeUrl()}");
            progressTracker.patchedNodeCount += 1;
        }

        public void ApplyingDelete(UrlDir.UrlConfig original, UrlDir.UrlConfig patch)
        {
            logger.Info($"Applying delete {patch.SafeUrl()} to {original.SafeUrl()}");
            progressTracker.patchedNodeCount += 1;
        }

        public void PatchApplied()
        {
            progressTracker.appliedPatchCount += 1;
        }

        public void NeedsUnsatisfiedRoot(UrlDir.UrlConfig url)
        {
            logger.Info($"Deleting root node in file {url.parent.url} node: {url.type} as it can't satisfy its NEEDS");
            progressTracker.needsUnsatisfiedCount += 1;
            progressTracker.needsUnsatisfiedRootCount += 1;
        }

        public void NeedsUnsatisfiedNode(UrlDir.UrlConfig url, NodeStack path)
        {
            logger.Info($"Deleting node in file {url.parent.url} subnode: {path.GetPath()} as it can't satisfy its NEEDS");
            progressTracker.needsUnsatisfiedCount += 1;
        }

        public void NeedsUnsatisfiedValue(UrlDir.UrlConfig url, NodeStack path, string valName)
        {
            logger.Info($"Deleting value in file {url.parent.url} subnode: {path.GetPath()} value: {valName} as it can't satisfy its NEEDS");
            progressTracker.needsUnsatisfiedCount += 1;
        }

        public void NeedsUnsatisfiedBefore(UrlDir.UrlConfig url)
        {
            logger.Info($"Deleting root node in file {url.parent.url} node: {url.type} as it can't satisfy its BEFORE");
            progressTracker.needsUnsatisfiedCount += 1;
            progressTracker.needsUnsatisfiedRootCount += 1;
        }

        public void NeedsUnsatisfiedFor(UrlDir.UrlConfig url)
        {
            logger.Warning($"Deleting root node in file {url.parent.url} node: {url.type} as it can't satisfy its FOR (this shouldn't happen)");
            progressTracker.needsUnsatisfiedCount += 1;
            progressTracker.needsUnsatisfiedRootCount += 1;
        }

        public void NeedsUnsatisfiedAfter(UrlDir.UrlConfig url)
        {
            logger.Info($"Deleting root node in file {url.parent.url} node: {url.type} as it can't satisfy its AFTER");
            progressTracker.needsUnsatisfiedCount += 1;
            progressTracker.needsUnsatisfiedRootCount += 1;
        }

        public void Error(UrlDir.UrlConfig url, string message)
        {
            progressTracker.errorCount += 1;
            logger.Error(message);
            RecordErrorFile(url);
        }

        public void Exception(string message, Exception exception)
        {
            progressTracker.exceptionCount += 1;
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
