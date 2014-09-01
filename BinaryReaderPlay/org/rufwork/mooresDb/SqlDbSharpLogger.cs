using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace org.rufwork.mooresDb
{
    public static class SqlDbSharpLogger
    {
        // TODO: Set up with a delegate so we can pipe this stuff wherever we want.
        public static void LogMessage(string strMessage, string strSource)
        {
            string strPayload = "INFO: Timestamp UTC: " + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + Environment.NewLine
                + strMessage + Environment.NewLine
                + Environment.NewLine
                + "\t" + strSource
                + Environment.NewLine
                + Environment.NewLine;

            Console.WriteLine(strPayload);
        }

    }
}
