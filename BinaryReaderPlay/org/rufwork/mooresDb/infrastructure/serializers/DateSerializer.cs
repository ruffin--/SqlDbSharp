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
    class DateSerializer : BaseSerializer
    {
        // inherited Column colRelated

        // Apparently I need to "manually chain" constructors?
        // http://stackoverflow.com/a/7230589/1028230
        public DateSerializer(Column colToUseAsBase)
            : base(colToUseAsBase)
        {
            this.colRelated = colToUseAsBase;
        }

        public override COMPARISON_TYPE? CompareBytesToVal(byte[] abytValFromRow, object objComparisonVal)
        {
            COMPARISON_TYPE? comparisonOutcome = null;
            DateTime dteTemp;

            if (!DateTime.TryParse(objComparisonVal.ToString(), out dteTemp))
            {
                throw new Exception("Illegal time value for comparison: " + objComparisonVal.ToString());
            }
            long lngComparison = dteTemp.Ticks;

            // Handle raw value from table.
            long lngFromRow = Utils.ByteArrayToLong(abytValFromRow);

            if (lngFromRow > lngComparison)
            {
                comparisonOutcome = COMPARISON_TYPE.GREATER_THAN;
            }
            else if (lngFromRow < lngComparison)
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
            DateTime dteTemp;

            if (strToSerialize.Equals("NOW()", StringComparison.CurrentCultureIgnoreCase))
            {
                dteTemp = DateTime.Now;
            }
            else
            {
                strToSerialize = strToSerialize.TrimEnd('\'').TrimStart('\'');

                if (!DateTime.TryParse(strToSerialize, out dteTemp))
                {
                    throw new Exception("Illegal date format: " + strToSerialize);
                }
            }

            long lngTicks = dteTemp.Ticks;
            byte[] abytSerialized = Utils.LongToByteArray(lngTicks);

            // because some datetimes might be fewer bytes that are required, we may have to pad
            // things out a bit so we don't confuse INSERT's if (abytVal.Length != colFound.intColLength)
            // check.
            abytToReturn = new byte[8];
            Buffer.BlockCopy(abytSerialized, 0, abytToReturn, 8 - abytSerialized.Length, abytSerialized.Length);

            return abytToReturn;
        }

        public override string toString(byte[] abytValue)
        {
            throw new NotImplementedException();
        }

        public override object toNative(byte[] abytValue)
        {
            long lngTicks = Utils.ByteArrayToLong(abytValue);
            DateTime dteReturn = new DateTime(lngTicks);

            return dteReturn;
        }
    }
}
