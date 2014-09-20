using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace org.rufwork.extensions
{
    public static class ObjectExtensions
    {
        public static string ToStringNullSafe(this object obj)
        {
            return (obj ?? string.Empty).ToString();
        }
    }
}
