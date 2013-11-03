// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using org.rufwork.utils;
using org.rufwork.mooresDb.infrastructure.tableParts;
using org.rufwork.mooresDb.infrastructure.serializers;

namespace org.rufwork.mooresDb.infrastructure
{
    public enum GROUP_TYPE { OR, AND };

    // This really is in no way a Comparison, but 
    // I want to have, eg, a List of Comparison that
    // holds only Comparison or this.
    public class CompoundComparison : Comparison
    {
        public List<Comparison> lstComparisons;
        public GROUP_TYPE groupType;
        
        public CompoundComparison(GROUP_TYPE pgroupType)
        {
            this.groupType = pgroupType;
            this.lstComparisons = new List<Comparison>();
        }
    }

    public class Comparison
    {
        //public string strColumnName;
        public Column colRelated;
        public COMPARISON_TYPE comparisonType;
        public byte[] abytComparisonValue;
        public object objParsedValue;

        public Comparison()
        {
        }

        public Comparison(char chrOperator, Column relatedColumn, byte[] abytValue)
        {
            COMPARISON_TYPE type = COMPARISON_TYPE.EQUALS;

            switch (chrOperator)
            {
                case '=':
                    type = COMPARISON_TYPE.EQUALS;
                    break;

                case '<':
                    type = COMPARISON_TYPE.LESS_THAN;
                    break;

                case '>':
                    type = COMPARISON_TYPE.GREATER_THAN;
                    break;

                default:
                    throw new Exception("Illegal comparison type: " + chrOperator);
            }

            _init(type, relatedColumn, abytValue);
        }

        public Comparison(COMPARISON_TYPE type, Column relatedColumn, byte[] abytValue)
        {
            _init(type, relatedColumn, abytValue);
        }

        private void _init(COMPARISON_TYPE type, Column relatedColumn, byte[] abytValue)
        {
            this.comparisonType = type;
            //this.strColumnName = strColName;
            this.colRelated = relatedColumn;
            this.abytComparisonValue = abytValue;

            this.objParsedValue = Router.routeMe(this.colRelated).toNative(abytValue);
        }
    }
}
