using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

using org.rufwork.extensions;
using org.rufwork.mooresDb.infrastructure.contexts;

namespace org.rufwork.mooresDb.infrastructure.commands
{
    // The easiest way to do this insanely quickly for a single column
    // is to use a full SelectCommand and then pull back the max.
    // TODO: LINQ your way to GROUP BY as well.
    public class SelectMaxCommand
    {
        private DatabaseContext _database;

        public SelectMaxCommand (DatabaseContext database)
        {
            _database = database;
        }

        // TODO: Create Command interface
        public object executeStatement(string strSql)
        {
            object objReturn = null;

            if (strSql.ToUpper().ContainsOutsideOfQuotes("GROUP BY"))
            {
                throw new NotImplementedException("GROUP BY clauses are not yet supported in MAX calls.");
            }

            int intParenIndex = strSql.IndexOf('(');
            string maxlessSql = strSql.Substring(intParenIndex + 1);

            string strFieldName = maxlessSql.Substring(0, maxlessSql.IndexOf(')')).ScrubValue();
            maxlessSql = "SELECT " + strFieldName + " " + maxlessSql.Substring(maxlessSql.IndexOf(')') + 1).Trim();

            SelectCommand selectCommand = new SelectCommand(_database);
            object objTable = (DataTable)selectCommand.executeStatement(maxlessSql);
            if (null != objTable)
            {
                DataTable table = (DataTable)objTable;
                if (table.Rows.Count > 0)
                {
                    table.DefaultView.Sort = strFieldName + " DESC";
                    table = table.DefaultView.ToTable();
                    objReturn = table.Rows[0][strFieldName];
                }
            }

            return objReturn;
        }
    }
}
