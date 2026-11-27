using System;

namespace ModuleManager
{
    public interface IUrlConfigIdentifier
    {
        string FileUrl { get; }
        string NodeType { get; }
        string FullUrl { get; }
    }

    public interface IProtoUrlConfig : IUrlConfigIdentifier
    {
        UrlDir.UrlFile UrlFile { get; }
        ConfigNode Node { get; }
    }

    public class ProtoUrlConfig : IProtoUrlConfig
    {
        public UrlDir.UrlFile UrlFile { get; }
        public ConfigNode Node { get; }
        public string FileUrl { get; }
        public string NodeType => Node.name;
        public string FullUrl { get; }

        // The node's name value, or null if it had none when this was created, so that NodeMatcher
        // need not look it up again for every patch. Holds the Value object because a special
        // command (*@PART[foo]/name = bar) writes to it in place. Edit and copy patches can add or
        // remove name values, but they modify a copy and wrap it in a new ProtoUrlConfig.
        private readonly ConfigNode.Value nameValue;
        public string NameValue => nameValue?.value;

        public ProtoUrlConfig(UrlDir.UrlFile urlFile, ConfigNode node)
        {
            UrlFile = urlFile ?? throw new ArgumentNullException(nameof(urlFile));
            Node = node ?? throw new ArgumentNullException(nameof(node));
            FileUrl = UrlFile.url + '.' + urlFile.fileExtension;
            FullUrl = FileUrl + '/' + Node.name;

            for (int i = 0; i < node.values.Count; ++i)
            {
                if (node.values[i].name == "name")
                {
                    nameValue = node.values[i];
                    break;
                }
            }

            if (NameValue != null)
                FullUrl += '[' + NameValue + ']';
        }
    }
}
