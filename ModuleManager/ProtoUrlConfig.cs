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

        public ProtoUrlConfig(UrlDir.UrlFile urlFile, ConfigNode node)
        {
            UrlFile = urlFile ?? throw new ArgumentNullException(nameof(urlFile));
            Node = node ?? throw new ArgumentNullException(nameof(node));
            FileUrl = UrlFile.url + '.' + urlFile.fileExtension;
            FullUrl = FileUrl + '/' + Node.name;
        }
    }
}
