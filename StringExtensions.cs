using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniLISP
{
    public static class StringExtensions
    {
        public static string F(this string format, params object[] args)
        {
            return String.Format(format, args);
        }
    }
}
