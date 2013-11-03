// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using org.rufwork;
using org.rufwork.mooresDb.infrastructure.tableParts;
using org.rufwork.utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace org.rufwork.mooresDb.infrastructure.serializers
{
    class IntSerializer : BaseSerializer
    {
        // inherited Column colRelated

        // Apparently I need to "manually chain" constructors?
        // http://stackoverflow.com/a/7230589/1028230
        public IntSerializer(Column colToUseAsBase)
            : base(colToUseAsBase)
        {
            this.colRelated = colToUseAsBase;
        }

        public override COMPARISON_TYPE? CompareBytesToVal(byte[] abytFromTable, object objComparisonVal)
        {
            int intFromTable = Utils.byteArrayToInt(abytFromTable);
            int intComparisonVal;
            COMPARISON_TYPE? comparisonOutcome = null;

            if (!int.TryParse(objComparisonVal.ToString(), out intComparisonVal))
            {
                throw new Exception("Illegal comparison value for an int: " + objComparisonVal.ToString());
            }

            if (intFromTable > intComparisonVal)
            {
                comparisonOutcome = COMPARISON_TYPE.GREATER_THAN;
            }
            else if (intFromTable < intComparisonVal)
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
            byte[] abytToReturn = null;
            int intValue;

            if (!int.TryParse(strToSerialize, out intValue))
            {
                throw new Exception("Invalid INT value for " + this.colRelated.strColName);
            }
            if (4 < this.colRelated.intColLength)
            {
                throw new Exception("INT column length is hard-coded to a max of four.");
            }
            else
            {
                abytToReturn = Utils.intToByteArray(intValue, this.colRelated.intColLength);
            }

            return abytToReturn;
        }

        public override string toString(byte[] abytValue)
        {
            throw new NotImplementedException();
        }

        public override object toNative(byte[] abytValue)
        {
            return Utils.byteArrayToInt(abytValue);
        }

    }
}
