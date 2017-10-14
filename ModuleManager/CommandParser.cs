using System;

namespace ModuleManager
{
    public static class CommandParser
    {
        public static Command Parse(string name, out string valueName)
        {
            if (name.Length == 0)
            {
                valueName = string.Empty;
                return Command.Insert;
            }
            Command ret;
            switch (name[0])
            {
                case '@':
                    ret = Command.Edit;
                    break;

                case '%':
                    ret = Command.Replace;
                    break;

                case '-':
                case '!':
                    ret = Command.Delete;
                    break;

                case '+':
                case '$':
                    ret = Command.Copy;
                    break;

                case '|':
                    ret = Command.Rename;
                    break;

                case '#':
                    ret = Command.Paste;
                    break;

                case '*':
                    ret = Command.Special;
                    break;

                case '&':
                    ret = Command.Create;
                    break;

                default:
                    valueName = name;
                    return Command.Insert;
            }
            valueName = name.Substring(1);
            return ret;
        }
    }
}
