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
    class DecimalSerializer : BaseSerializer
    {
        // inherited Column colRelated

        // Apparently I need to "manually chain" constructors?
        // http://stackoverflow.com/a/7230589/1028230
        public DecimalSerializer(Column colToUseAsBase)
            : base(colToUseAsBase)
        {
            this.colRelated = colToUseAsBase;
        }

        public override COMPARISON_TYPE? CompareBytesToVal(byte[] abytValFromRow, object objNative)
        {
            COMPARISON_TYPE? comparisonOutcome = null;

            // TODO: What other types are okay?
            if (objNative.GetType() != typeof(decimal)) 
            {
                throw new Exception("Unexpected value to compare against a CHAR type column: " + objNative.GetType());
            }
            decimal decFromWhere = (decimal)objNative;

            // Handle raw value from table.
            decimal decFromRow;
            string strRawToString = this.toNative(abytValFromRow).ToString();

            if (!decimal.TryParse(strRawToString, out decFromRow))
            {
                throw new Exception("Illegal value from table.");
            }

            if (decFromRow > decFromWhere)
            {
                comparisonOutcome = COMPARISON_TYPE.GREATER_THAN;
            }
            else if (decFromRow < decFromWhere)
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
            byte[] abytVal = null;

            // sign bit, then count of bytes before decimal insertion.
            byte bytSignAndPlaces = 0;

            // TODO: I should probably do byte mathes here, just to be
            // clear what I'm doing.  But I'm not.
            if (strToSerialize.StartsWith("-"))
            {
                bytSignAndPlaces += 128;    // set negative flag.
            }

            string[] astrSplitOnDecimal = strToSerialize.Split('.');
            if (astrSplitOnDecimal.Length > 2)
            {
                throw new Exception("Illegally formatted number for INSERT: " + strToSerialize);
            }

            int intWholeNumber;
            byte[] abytWholeNumber;
            int intDecimalVal = 0;
            byte[] abytDecimalVal;

            // TODO: Capture significant digits.
            if (!int.TryParse(astrSplitOnDecimal[0], out intWholeNumber))
            {
                throw new Exception("Illegal whole number in INSERT: " + strToSerialize);
            }
            if (bytSignAndPlaces > 127)
            {
                intWholeNumber *= -1;   // if it's negative, set to positive to get serialization bytes.  Else it'll be huge.
            }
            abytWholeNumber = Utils.IntToByteArray(intWholeNumber);

            // Note that we already checked to make sure !(length > 2) already.
            if (2 == astrSplitOnDecimal.Length)
            {
                if (!int.TryParse(astrSplitOnDecimal[1], out intDecimalVal))
                {
                    throw new Exception("Illegal decimal value: " + strToSerialize);
                }
            }
            abytDecimalVal = Utils.IntToByteArray(intDecimalVal);
            bytSignAndPlaces += (byte)abytDecimalVal.Length;    // I think it's easier to count the spaces for the decimal (than the whole number), 
            // then count from the right when reconstituting.  Means we're limited to 127 places 
            // *after* the decimal, which isn't too bad, right?

            // So now we need to make sure it'll fit into the column as initially lengthed.
            // Note that there are ways to make this simply round if things are too big to be
            // perfectly saved, but we're only saving "perfect" values for the time being.
            // TODO: If we need 2.123 * 10^200, we'll handle that later.
            // TODO: I'm not sure if the -1 to the col length is intuitive.  Perhaps we should
            // store the number in intColLength and just know to add one extra byte each row
            // in CreateTable.
            if (abytWholeNumber.Length + abytDecimalVal.Length > this.colRelated.intColLength - 1)
            {
                // Now there are other ways to determine if we were out of range earlier, even figuring out
                // max values based on the length of the column, and then parsing the string first.  That's
                // probably more efficient on some level, but this isn't a horribly resource intensive task
                // to get to this point.
                throw new Exception("Value for column out of range: " + this.colRelated.strColName + " :: " + strToSerialize);
            }

            // We only have seven bits to store the number of bytes after the decimal.  It's going to be rare
            // that we overshoot this, I think, but it's certainly possible, and deserves a check.
            if (abytDecimalVal.Length > 127)
            {
                throw new Exception("Decimal value is too large to be stored in this column: " + this.colRelated.strColName + " :: " + strToSerialize);
            }

            // TODO: This seems like too many arrays to instantiate.  Consider ways to clean
            // up that aren't overly convoluted.
            byte[] abytCombined = new byte[abytWholeNumber.Length + abytDecimalVal.Length];
            abytWholeNumber.CopyTo(abytCombined, 0);
            abytDecimalVal.CopyTo(abytCombined, abytWholeNumber.Length);

            if (abytCombined.Length > this.colRelated.intColLength)
            {
                throw new Exception("Value is too large for insert into column: " + this.colRelated.strColName + " :: " + strToSerialize);
            }

            abytVal = new byte[this.colRelated.intColLength];
            Buffer.BlockCopy(abytCombined, 0, abytVal, abytVal.Length - abytCombined.Length, abytCombined.Length);
            abytVal[0] = bytSignAndPlaces;  // we shouldn't be overwriting anything here based on the checks we did earlier.

            return abytVal;
        }

        public override string ToString()
        {
             return base.ToString();
        }

        public override object toNative(byte[] abytValue)
        {
            int isNegative = 1;

            int intSignAndPlaces = abytValue[0];
            if (intSignAndPlaces > 128)
            {
                isNegative = -1;
                intSignAndPlaces = intSignAndPlaces - 128;
            }

            int intCutOffPoint = abytValue.Length - intSignAndPlaces - 1;
            byte[] abytWholeNumber = new byte[intCutOffPoint];
            byte[] abytDecimal = new byte[intSignAndPlaces];

            Array.Copy(abytValue, 1, abytWholeNumber, 0, intCutOffPoint);
            Array.Copy(abytValue, intCutOffPoint + 1, abytDecimal, 0, intSignAndPlaces);

            // Make sure you get this (from Skeet): The decimal type doesn't normalize itself - it remembers how many decimal digits it has (by maintaining the exponent where possible) and on formatting, zero may be counted as a significant decimal digit.
            int intWholeNumber = Utils.ByteArrayToInt(abytWholeNumber, true);
            int intDecimalVal = Utils.ByteArrayToInt(abytDecimal, true);

            // There got to be a smarter way to do this.  I'm being lazy right now
            // by pretending to be clever.
            decimal decOut;
            decimal.TryParse(intWholeNumber + "." + intDecimalVal, out decOut);

            return isNegative * decOut;
        }

    }
}
