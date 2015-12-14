using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace org.rufwork.shims
{
    public static class PCLConsole
    {
        public static string StrDateStamp
        {
            get
            {
                return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            }
        }

        public static void WriteLine(string strMsg)
        {
            System.Diagnostics.Debug.WriteLine(PCLConsole.StrDateStamp + " -- " + strMsg);
        }

    }
}
