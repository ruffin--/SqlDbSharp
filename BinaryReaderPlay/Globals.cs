// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;

public static class Globals
{
    // Right now, on Windows, that's "C:\\Users\\YourUserName\\Documents\\MooresDbPlay"
    //public static readonly string cstrDbDir = Utils.cstrHomeDir + Path.DirectorySeparatorChar + "MooresDbPlay";
    public static bool bDebug = false;
        public static string buildData = "20150814";    // not always incremented with each build.
        public static string version = "0.0.4.4w";
}
