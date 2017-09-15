using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ModuleManager.Extensions
{
    public static class StringExtensions
    {
        public static bool IsBracketBalanced(this string s)
        {
            int level = 0;
            foreach (char c in s)
            {
                if (c == '[') level++;
                else if (c == ']') level--;

                if (level < 0) return false;
            }
            return level == 0;
        }
    }
}
