// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using org.rufwork.mooresDb.infrastructure.tableParts;
using org.rufwork.utils;

namespace org.rufwork.mooresDb.infrastructure.serializers
{
    public class BaseSerializer
    {
        public virtual Column colRelated { get; set; }

        // Comparison objects have already unwrapped the comparison value, so 
        // we're going to pass one byte array (probably from the raw table) and
        // one parsed value (likely from the comparsion object).
        public virtual COMPARISON_TYPE? CompareBytesToVal(byte[] abytFromTable, object objComparisonVal)
        {
            throw new Exception("override CompareBytesToVal");
        }

        public BaseSerializer(Column colToUseAsBase)
        {
            this.colRelated = colRelated;
        }

        public virtual byte[] toByteArray(string strToSerialize)
        {
            throw new Exception("override toByteArray please");
        }
        public virtual string toString(byte[] abytValue)
        {
            throw new Exception("override toString please");
        }
        public virtual object toNative(byte[] abytValue)
        {
            throw new Exception("override toNative please");
        }
        // TODO: Should allow and route any operation through here.
        public virtual byte[] addRawToStringRepresentation(byte[] abytRaw, string strValToAdd, bool useNegative = false)
        {
            throw new Exception("override addRawToStringRepresentation please");
        }
        public virtual byte[] addRawToRaw(byte[] abytRaw, byte[] abytToAdd, bool useNegative = false)
        {
            throw new Exception("override addRawToRaw please");
        }
    }
}
