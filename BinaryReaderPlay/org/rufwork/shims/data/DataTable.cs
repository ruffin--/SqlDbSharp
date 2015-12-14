using System;
using System.Linq;
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

        public DataRow[] Select()
        {
            return this.Rows.ToArray();
        }

        #region Wholly owned code stolen from rufwork extensions for "real" DataTable.
        /// <summary>
        /// This method will take the original DataTable and return another
        /// DataTable with the as-specified first `n` rows from the original in it.
        /// Note that the original DataTable needs to, obviously, already be sorted.
        /// TODO: There's got to be a better way to do this.
        /// </summary>
        /// <param name="t">The `this` Datatable</param>
        /// <param name="n">The number of rows to take.</param>
        /// <returns></returns>
        public DataTable SelectTopNRows(int n)
        {
            return this.SkipTakeToTable(0, n);
        }

        public DataTable SkipTakeToTable(int intSkip, int intTake)
        {
            DataRow[] aRowsAll = this.Select();
            DataRow[] aRowsTake = aRowsAll.Skip(intSkip).Take(intTake).ToArray();

            return _copyToDataTable(aRowsTake);
        }

        private DataTable _copyToDataTable(DataRow[] rows)
        {
            DataTable table = new DataTable();

            if (rows.Length > 1)
            {
                foreach (DataColumn col in this.Columns)
                {
                    DataColumn colNew = new DataColumn(col.ColumnName);
                    colNew.DataType = col.DataType;
                    table.Columns.Add(colNew);
                }

                foreach (DataRow existingRow in rows)
                {
                    DataRow newRow = table.NewRow();
                    foreach (DataColumn dc in this.Columns)
                    {
                        newRow[dc] = existingRow[dc.ColumnName];    // ????: I *think* I can't reuse the DataColumn here, maybe? Is dc === dc2?
                    }
                }
            }

            return table;
        }
        #endregion Wholly owned code stolen from rufwork extensions for "real" DataTable.
    }
}

