// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using org.rufwork.shims;

public static class Globals
{
    // Right now, on Windows, that's "C:\\Users\\YourUserName\\Documents\\MooresDbPlay"
    //public static readonly string cstrDbDir = Utils.cstrHomeDir + Path.DirectorySeparatorChar + "MooresDbPlay";
    public static bool bDebug = false;
    public static string buildData = "20151213";    // not always incremented with each build.
    public static string version = "0.0.4.5";

    public static void logit(string strMsg)
    {
        if (Globals.bDebug)
        {
            PCLConsole.WriteLine(strMsg);
        }
    }
}
