using System;
using System.Collections.Generic;
using NodeStack = ModuleManager.Collections.ImmutableStack<ConfigNode>;

namespace ModuleManager
{
    public interface IPatchProgress
    {
        int AppliedPatchCount { get; }
        int ErrorCount { get; }
        int ExceptionCount { get; }
        int NeedsUnsatisfiedCount { get; }
        int PatchedNodeCount { get; set; }
        float ProgressFraction { get; }
        int TotalPatchCount { get; }
        Dictionary<String, int> ErrorFiles { get; }

        void Error(UrlDir.UrlConfig url, string message);
        void Exception(string message, Exception exception);
        void Exception(UrlDir.UrlConfig url, string message, Exception exception);
        void NeedsUnsatisfiedRoot(UrlDir.UrlConfig url);
        void NeedsUnsatisfiedNode(UrlDir.UrlConfig url, NodeStack path);
        void NeedsUnsatisfiedValue(UrlDir.UrlConfig url, NodeStack path, string valName);
        void ApplyingCopy(UrlDir.UrlConfig original, UrlDir.UrlConfig patch);
        void ApplyingDelete(UrlDir.UrlConfig original, UrlDir.UrlConfig patch);
        void ApplyingUpdate(UrlDir.UrlConfig original, UrlDir.UrlConfig patch);
        void PatchAdded();
        void PatchApplied();
    }
}
