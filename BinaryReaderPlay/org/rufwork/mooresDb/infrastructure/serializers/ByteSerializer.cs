// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================


// DEPRECATED/UNUSED.  (Need to replace with a real BitSerializer.)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using org.rufwork.mooresDb.infrastructure.tableParts;
using org.rufwork.utils;

namespace org.rufwork.mooresDb.infrastructure.serializers
{
    class ByteSerializer : BaseSerializer
    {
        // inherited Column colRelated

        // Apparently I need to "manually chain" constructors?
        // http://stackoverflow.com/a/7230589/1028230
        public ByteSerializer(Column colToUseAsBase)
            : base(colToUseAsBase)
        {
            this.colRelated = colToUseAsBase;
        }

        public override COMPARISON_TYPE? CompareBytesToVal(byte[] abytFromTable, object objComparisonVal)
        {
            if (abytFromTable.Length != 0)
            {
                throw new Exception("Illegal row value for byte comparison");
            }
            
            byte bytFromTable = abytFromTable[0];
            byte bytComparisonVal;
            COMPARISON_TYPE? comparisonOutcome = null;

            if (!byte.TryParse(objComparisonVal.ToString(), out bytComparisonVal))
            {
                throw new Exception("Illegal comparison value for an int: " + objComparisonVal.ToString());
            }

            if (bytFromTable > bytComparisonVal)
            {
                comparisonOutcome = COMPARISON_TYPE.GREATER_THAN;
            }
            else if (bytFromTable < bytComparisonVal)
            {
                comparisonOutcome = COMPARISON_TYPE.LESS_THAN;
            }
            else
            {
                comparisonOutcome = COMPARISON_TYPE.EQUALS;
            }

            return comparisonOutcome;
        }

        public override byte[] toByteArray(string strToSerialize)
        {
            byte bytVal;
            if (!byte.TryParse(strToSerialize, out bytVal))
            {
                throw new Exception("Illegal byte value: " + strToSerialize);
            }
            return new byte[1] { bytVal };
        }

        public override string ToString()
        {
             return base.ToString();
        }

        public override object toNative(byte[] abytValue)
        {
            if (abytValue.Length != 1)
            {
                throw new Exception("Illegal byte value to serialze: " + abytValue);
            }

            return abytValue;
        }
    }
}
