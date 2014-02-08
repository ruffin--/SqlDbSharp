using System;
using System.Collections.Generic;
using org.rufwork.collections;
using org.rufwork.shims.data.collections;

namespace org.rufwork.shims.data
{
    public class DataTable
    {
        public bool CaseSensitive = false;  // not actually live/used.
        public List<DataRow> Rows = new List<DataRow>();
        public DataColumnSet Columns = new DataColumnSet();

        public string TableName = string.Empty;
        public DataView DefaultView = null;

        public DataTable()
        {
            this.DefaultView = new DataView(this);
        }

        public DataTable(string strTableName)
        {
            this.TableName = strTableName;
        }

        public DataRow NewRow()
        {
            DataRow newRow = new DataRow(this.Columns);
            return newRow;
        }
    }
}

