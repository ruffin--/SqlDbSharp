// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace org.rufwork.mooresDb.exceptions
{
    public class ColumnNotFoundException : System.Exception
    {
        public ColumnNotFoundException() : base() { }
        public ColumnNotFoundException(string strAlert) : base(strAlert) { }
        public ColumnNotFoundException(string strAlert, Exception e) : base(strAlert, e) { }
    }
}