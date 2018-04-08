using System;

namespace ModuleManager
{
    public static class OperatorParser
    {
        public static Operator Parse(string name, out string valueName)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (name.Length == 0)
            {
                valueName = string.Empty;
                return Operator.Assign;
            }

            Operator ret;
            switch (name[name.Length - 1])
            {
                case '+':
                    ret = Operator.Add;
                    break;

                case '-':
                    ret = Operator.Subtract;
                    break;

                case '*':
                    ret = Operator.Multiply;
                    break;

                case '/':
                    ret = Operator.Divide;
                    break;

                case '!':
                    ret = Operator.Exponentiate;
                    break;

                case '^':
                    ret = Operator.RegexReplace;
                    break;

                default:
                    valueName = name;
                    return Operator.Assign;
            }
            valueName = name.Substring(0, name.Length - 1).TrimEnd();
            return ret;
        }
    }
}
