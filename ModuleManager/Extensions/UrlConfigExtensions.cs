using System;
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
            else if (url.type == null)
            {
                nodeName = "<null>";
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

        public static string PrettyPrint(this UrlDir.UrlConfig config)
        {
            if (config == null) return "<null UrlConfig>";

            StringBuilder sb = new StringBuilder();

            sb.Append(config.SafeUrl());
            sb.Append('\n');
            config.config.PrettyPrint(ref sb, "  ");
            
            return sb.ToString();
        }
    }
}
