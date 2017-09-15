using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModuleManager.Extensions
{
    public static class UrlConfigExtensions
    {
        public static string SafeUrl(this UrlDir.UrlConfig url)
        {
            if (url == null) return "<null>";

            string nodeName;

            if (!string.IsNullOrEmpty(url.type?.Trim()))
            {
                nodeName = url.type;
            }
            else if (!string.IsNullOrEmpty(url.config?.name?.Trim()))
            {
                nodeName = url.config.name;
            }
            else
            {
                nodeName = "<blank>";
            }

            string parentUrl = null;

            if (url.parent != null)
            {
                try
                {
                    parentUrl = url.parent.url;
                }
                catch
                {
                    parentUrl = "<unknown>";
                }
            }

            if (parentUrl == null)
                return nodeName;
            else
                return parentUrl + "/" + nodeName;
        }
    }
}
