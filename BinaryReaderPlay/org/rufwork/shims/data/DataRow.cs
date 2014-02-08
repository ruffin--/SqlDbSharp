using org.rufwork.shims.data.collections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace org.rufwork.shims.data
{
    public class DataRow : Dictionary<DataColumn, object>
    {
        public DataRow(DataColumnSet columns)
        {
            foreach (DataColumn col in columns)
            {
                this.Add(col, null);
            }
        }

        private int _findIndexByColName(string strColName)
        {
            int intColFound = -1;
            for (int i = 0; i < this.Count(); i++)
            {
                if (strColName.Equals(this.ElementAt(i).Key.ColumnName, StringComparison.CurrentCultureIgnoreCase))
                {
                    intColFound = i;
                    break;
                }
            }
            return intColFound;
        }

        public object this[int intIndex]
        {
            get
            {
                return this.ElementAt(intIndex).Value;
            }
            set
            {
                DataColumn dc = this.ElementAt(intIndex).Key;
                this[dc] = value;
            }
        }

        public object this[string strColName]
        {
            get
            {
                int intCol = _findIndexByColName(strColName);
                if (intCol < 0)
                    throw new Exception("Column " + strColName + " does not exist in this row.");
                return this[intCol];
            }

            set
            {
                int intCol = _findIndexByColName(strColName);
                if (intCol < 0)
                    throw new Exception("Column " + strColName + " does not exist in this row.");
                this[intCol] = value;

            }
        }
    }
}

