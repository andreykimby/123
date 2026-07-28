using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skarynagram.Utils
{
    public static class StringSanitizer
    {
        public static string MaskToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return "***";
            return token.Length > 6
                ? token.Substring(0, 3) + "..." + token.Substring(token.Length - 3)
                : "***";
        }
    }
}
