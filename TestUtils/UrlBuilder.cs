using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TestUtils
{
    public static class UrlBuilder
    {
        private static readonly FieldInfo UrlDir__field__type;
        private static readonly FieldInfo UrlDir__field__name;
        private static readonly FieldInfo UrlDir__field__root;
        private static readonly FieldInfo UrlDir__field__parent;

        private static readonly FieldInfo UrlFile__field__name;
        private static readonly FieldInfo UrlFile__field__fileType;
        private static readonly FieldInfo UrlFile__field__fileExtension;

        static UrlBuilder()
        {
            FieldInfo[] UrlDirFields = typeof(UrlDir).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo[] UrlDirFields__DirectoryType = UrlDirFields.Where(field => field.FieldType == typeof(UrlDir.DirectoryType)).ToArray();
            FieldInfo[] UrlDirFields__string = UrlDirFields.Where(field => field.FieldType == typeof(string)).ToArray();
            FieldInfo[] UrlDirFields__UrlDir = UrlDirFields.Where(field => field.FieldType == typeof(UrlDir)).ToArray();

            UrlDir__field__type = UrlDirFields__DirectoryType[0];
            UrlDir__field__name = UrlDirFields__string[0];
            UrlDir__field__root = UrlDirFields__UrlDir[0];
            UrlDir__field__parent = UrlDirFields__UrlDir[1];

            FieldInfo[] UrlFileFields = typeof(UrlDir.UrlFile).GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo[] UrlFileFields__string = UrlFileFields.Where(field => field.FieldType == typeof(string)).ToArray();
            FieldInfo[] UrlFileFields__FileType = UrlFileFields.Where(field => field.FieldType == typeof(UrlDir.FileType)).ToArray();

            UrlFile__field__name = UrlFileFields__string[0];
            UrlFile__field__fileExtension = UrlFileFields__string[2];
            UrlFile__field__fileType = UrlFileFields__FileType[0];
        }

        public static UrlDir CreateRoot()
        {
            return new UrlDir(new UrlDir.ConfigDirectory[0], new UrlDir.ConfigFileType[0]);
        }

        public static UrlDir CreateDir(string url, UrlDir parent = null)
        {
            if (parent == null)
            {
                parent = CreateRoot();
            }
            else
            {
                UrlDir existingDir = parent.GetDirectory(url);
                if (existingDir != null) return existingDir;
            }

            UrlDir current = parent;

            foreach(string name in url.Split('/'))
            {
                UrlDir dir = CreateRoot();
                UrlDir__field__name.SetValue(dir, name);
                UrlDir__field__root.SetValue(dir, current.root);
                UrlDir__field__parent.SetValue(dir, current);

                current.children.Add(dir);
                current = dir;
            }

            return current;
        }

        public static UrlDir CreateGameData(UrlDir root = null)
        {
            if (root == null)
            {
                root = CreateRoot();
            }
            else
            {
                UrlDir potentialGameData = root.children.FirstOrDefault(dir => dir.type == UrlDir.DirectoryType.GameData && dir.name == "");
                if (potentialGameData != null) return potentialGameData;
            }

            UrlDir gameData = CreateRoot();
            UrlDir__field__name.SetValue(gameData, "");
            UrlDir__field__type.SetValue(gameData, UrlDir.DirectoryType.GameData);
            UrlDir__field__root.SetValue(gameData, root);
            UrlDir__field__parent.SetValue(gameData, root);

            root.children.Add(gameData);

            return gameData;
        }

        public static UrlDir.UrlFile CreateFile(string path, UrlDir parent = null)
        {
            int sepIndex = path.LastIndexOf('/');
            string name = path;

            if (sepIndex != -1)
            {
                parent = CreateDir(path.Substring(0, sepIndex), parent);
                name = path.Substring(sepIndex + 1);
            }
            else if (parent == null)
            {
                parent = CreateRoot();
            }

            string nameWithoutExtension = Path.GetFileNameWithoutExtension(name);
            string extension = Path.GetExtension(name);
            if (!string.IsNullOrEmpty(extension)) extension = extension.Substring(1);

            UrlDir.UrlFile existingFile = parent.files.FirstOrDefault(f => f.name == nameWithoutExtension && f.fileExtension == extension);
            if (existingFile != null) return existingFile;

            bool cfg = false;
            string newName = name;

            if (extension == "cfg")
            {
                cfg = true;
                newName = name + ".not_cfg";
            }

            UrlDir.UrlFile file = new UrlDir.UrlFile(parent, new FileInfo(newName));

            if (cfg)
            {
                UrlFile__field__name.SetValue(file, nameWithoutExtension);
                UrlFile__field__fileExtension.SetValue(file, "cfg");
                UrlFile__field__fileType.SetValue(file, UrlDir.FileType.Config);
            }

            parent.files.Add(file);

            return file;
        }

        public static UrlDir.UrlConfig CreateConfig(ConfigNode node, UrlDir.UrlFile parent)
        {
            UrlDir.UrlConfig config = new UrlDir.UrlConfig(parent, node);
            parent.configs.Add(config);
            return config;
        }

        public static UrlDir.UrlConfig CreateConfig(string url, ConfigNode node, UrlDir parent = null)
        {
            if (Path.GetExtension(url) != ".cfg") url += ".cfg";

            UrlDir.UrlFile file = CreateFile(url, parent);

            return CreateConfig(node, file);
        }
    }
}
