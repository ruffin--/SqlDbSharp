// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace org.rufwork.extensions
{
    public static class DateTimeNullableExtensions
    {
        public static string ToStringExt(this DateTime? dte, string strFormatter = "yyyy-MM-dd HH:mm:ss")
        {
            return dte.HasValue ? dte.Value.ToString(strFormatter) : string.Empty;
        }
    }
}
