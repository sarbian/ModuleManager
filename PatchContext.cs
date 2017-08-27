using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModuleManager
{
    public struct PatchContext
    {
        public readonly UrlDir.UrlConfig patchUrl;
        public readonly UrlDir databaseRoot;

        public PatchContext(UrlDir.UrlConfig patchUrl, UrlDir databaseRoot)
        {
            this.patchUrl = patchUrl;
            this.databaseRoot = databaseRoot;
        }
    }
}
