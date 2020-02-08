using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

// Will need to be removed if we go PCL.
namespace org.rufwork.utils
{
    public static class DataTableExtensions
    {
        /// <summary>
        /// This method will take the original DataTable and return another
        /// DataTable with the as-specified first `n` rows from the original in it.
        /// Note that the original DataTable needs to, obviously, already be sorted.
        /// TODO: There's got to be a better way to do this.
        /// </summary>
        /// <param name="t">The `this` Datatable</param>
        /// <param name="n">The number of rows to take.</param>
        /// <returns></returns>
        public static DataTable SelectTopNRows(this DataTable t, int n)
        {
            return t.SkipTakeToTable(0, n);
        }

        public static DataTable SkipTakeToTable(this DataTable t, int intSkip, int intTake)
        {
            DataRow[] aRowsAll = t.Select();
            DataRow[] aRowsTake = aRowsAll.Skip(intSkip).Take(intTake).ToArray();

            return aRowsTake.CopyToDataTable();
        }
    }
}
