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

        public static string PrettyPrint(this UrlDir.UrlConfig config)
        {
            if (config == null) return "<null UrlConfig>";

            StringBuilder sb = new StringBuilder();

            if (config.type == null) sb.Append("<null type>");
            else sb.Append(config.type);

            if (config.name == null) sb.Append("[<null name>]");
            else if (config.name != config.type) sb.AppendFormat("[{0}]", config.name);

            sb.Append('\n');
            config.config.PrettyPrint(ref sb, "  ");
            
            return sb.ToString();
        }
    }
}
