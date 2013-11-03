// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using org.rufwork.mooresDb.infrastructure.tableParts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using org.rufwork.utils;

namespace org.rufwork.mooresDb.infrastructure.serializers
{
    class CharSerializer : BaseSerializer
    {
        // inherited Column colRelated

        // Apparently I need to "manually chain" constructors?
        // http://stackoverflow.com/a/7230589/1028230
        public CharSerializer(Column colToUseAsBase)
            : base(colToUseAsBase)
        {
            this.colRelated = colToUseAsBase;
        }

        public override COMPARISON_TYPE? CompareBytesToVal(byte[] abytValFromDB, object objNative)
        {
            COMPARISON_TYPE? comparisonOutcome = null;

            if (objNative.GetType() != typeof(String))
            {
                throw new Exception("Unexpected value to compare against a CHAR type column: " + objNative.GetType());
            }

            comparisonOutcome = this.CompareByteArrays(abytValFromDB, this.toByteArray(objNative.ToString()));

            return comparisonOutcome;
        }

        /// <summary>
        /// Takes two arrays and returns which is greater based on byte
        /// (always positive) values.  Note that this (obviously) isn't
        /// a good way to compare serialized integers, etc.
        /// TODO: Null values.
        /// </summary>
        public COMPARISON_TYPE CompareByteArrays(byte[] a1, byte[] a2)
        {
            COMPARISON_TYPE comparisonOutcome = COMPARISON_TYPE.EQUALS;

            if (a1.Length == a2.Length)
            {
                for (int i = 0; i < a1.Length; i++)
                {
                    if (a1[i] != a2[i])
                    {
                        if (a1[i] > a2[i])
                        {
                            comparisonOutcome = COMPARISON_TYPE.GREATER_THAN;
                        }
                        else
                        {
                            comparisonOutcome = COMPARISON_TYPE.LESS_THAN;
                        }
                        break;
                    }
                }
            }

            return comparisonOutcome;
        }

        public override byte[] toByteArray(string strValToSerialize)
        {
            string strNoQuotes = strValToSerialize;
            if (strValToSerialize.StartsWith("'") && strValToSerialize.EndsWith("'") && strValToSerialize.Length >= 2)
            {
                strNoQuotes = strValToSerialize.Substring(1, strValToSerialize.Length - 2);
            }

            // TODO: Handle escaped single quotes.
            byte[] abytVal = null;
            abytVal = Encoding.ASCII.GetBytes(strNoQuotes);

            byte[] abytColContents = new byte[this.colRelated.intColLength];   // we'll put the value into this "full length" array
            if (abytVal.Length <= this.colRelated.intColLength)
            {
                // Since the value we've parsed from the string/token culled from the SQL statement
                // might not be as long as the field allows, we're copying the bytes representing the
                // token's value into the [potentially] larger contents array to ensure that it fills
                // the space in the table file.  The balance of the ColContents array should have 0s.
                
                // public static void BlockCopy( Array src, int srcOffset, Array dst, int dstOffset, int count) 
                // source array, offset for starting in the source, destination array, offset for writing there, num of bytes to copy
                System.Buffer.BlockCopy(abytVal, 0, abytColContents, 0, abytVal.Length);
            }
            else
            {
                throw new Exception("INSERT fails; data too long for column: " + this.colRelated.strColName 
                    + " :: " + strValToSerialize);
            }

            return abytColContents;
        }

        public override string toString(byte[] abytValue)
        {
            throw new Exception("implement please");
        }

        public override object toNative(byte[] abytValue)
        {
            for (int i = 0; i < abytValue.Length; i++)
            {
                if (0x00 == abytValue[i])
                {
                    abytValue = abytValue.Take(i).ToArray();
                    break;
                }
            }
            return System.Text.Encoding.ASCII.GetString(abytValue);
        }
    }
}
