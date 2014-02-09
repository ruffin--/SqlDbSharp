// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using org.rufwork.shims.data;
using System.IO;
using System.Text.RegularExpressions;
using org.rufwork.mooresDb.infrastructure.tableParts;
using org.rufwork.mooresDb.infrastructure.serializers;
using org.rufwork.mooresDb.infrastructure.contexts;
using org.rufwork.mooresDb.infrastructure;
using org.rufwork.mooresDb.infrastructure.commands.Processors;
using org.rufwork.utils;

namespace org.rufwork.mooresDb.infrastructure.commands
{
    public class SelectCommand
    {
        private TableContext _table;
        private DatabaseContext _database;

        public SelectCommand (DatabaseContext database)
        {
            _database = database;
        }

        // TODO: Create Command interface
        public DataTable executeStatement(string strSql)
        {
            DataTable dtReturn = new DataTable();

            // TODO: Think how to track multiple tables/TableContexts
            // Note that the constructor will set up the table named
            // in the SELECT statement in _table.
            CommandParts selectParts = new CommandParts(_database, _table, strSql, CommandParts.COMMAND_TYPES.SELECT);

            if (MainClass.bDebug)
            {
                Console.WriteLine("SELECT: " + selectParts.strSelect);
                Console.WriteLine("FROM: " + selectParts.strFrom);
                if (!string.IsNullOrEmpty(selectParts.strInnerJoinKludge))
                {
                    Console.WriteLine("INNER JOIN: " + selectParts.strInnerJoinKludge);
                }
                Console.WriteLine("WHERE: " + selectParts.strWhere);    // Note that WHEREs aren't applied to inner joined tables right now.
                Console.WriteLine("ORDER BY: " + selectParts.strOrderBy);
            }

            _table = _database.getTableByName(selectParts.strTableName);
            dtReturn = _initDataTable(selectParts);

            WhereProcessor.ProcessRows(ref dtReturn, _table, selectParts);

            // (Joins are only in selects, so this isn't part of WhereProcessing.)
            //
            // To take account of joins, we basically need to create a SelectParts
            // per inner join.  So we need to create a WHERE from the table we 
            // just selected and then send those values down to a new _selectRows.
            if (selectParts.strInnerJoinKludge.Length > 0)
            {
                dtReturn = _processInnerJoin(dtReturn, selectParts.strInnerJoinKludge, selectParts.strTableName);
            }

            if (null != selectParts.strOrderBy && selectParts.strOrderBy.Length > 9)
            {
                dtReturn.DefaultView.Sort = selectParts.strOrderBy.Substring(9);
                dtReturn = dtReturn.DefaultView.ToTable();
            }

            return dtReturn;
        }

        private DataTable _processInnerJoin(DataTable dtReturn, string strJoinText, string strParentTable)
        {
            Console.WriteLine("Note that WHERE clauses are not yet applied to JOINed tables.");

            string strNewTable = null;
            string strNewField = null;
            string strOldField = null;
            string strOldTable = null;

            strJoinText = System.Text.RegularExpressions.Regex.Replace(strJoinText, @"\s+", " ");
            string[] astrInnerJoins = strJoinText.ToLower().Split(new string[] {"inner join"}, StringSplitOptions.RemoveEmptyEntries);
            Dictionary<string, DataTable> dictTables = new Dictionary<string, DataTable>();
            dictTables.Add(strParentTable.ToLower(), dtReturn);

            foreach (string strInnerJoin in astrInnerJoins)
            {
                // "from table1 inner join" <<< already removed
                // "table2 on table1.field = table2.field2"
                string[] astrTokens = strInnerJoin.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries);

                if (!"=".Equals(astrTokens[3]))
                {
                    throw new Exception("We're only supporting inner equi joins right now: " + strInnerJoin);
                }

                string field1Parent = astrTokens[2].Substring(0, astrTokens[2].IndexOf("."));
                string field2Parent = astrTokens[4].Substring(0, astrTokens[4].IndexOf("."));
                string field1 = astrTokens[2].Substring(astrTokens[2].IndexOf(".")+1);
                string field2 = astrTokens[4].Substring(astrTokens[4].IndexOf(".")+1);
                
                if (dictTables.ContainsKey(field1Parent))   // TODO: Should probably check to see if they're both known and at least bork.
                {
                    strNewTable = field2Parent;
                    strNewField = field2;
                    strOldTable = field1Parent;
                    strOldField = field1;
                }
                else
                {
                    strNewTable = field1Parent;
                    strNewField = field1;
                    strOldTable = field2Parent;
                    strOldField = field2;
                }

                string strInClause = "";

                dictTables[strOldTable].CaseSensitive = false;  // TODO: If we keep this, do it in a smarter place.
                // Right now, strOldField has the name that's in the DataTable from the previous select.
                foreach (DataRow row in dictTables[strOldTable].Rows)
                {
                    strInClause += row[strOldField].ToString() + ",";
                }
                strInClause = strInClause.Trim (',');

                string strInnerSelect = "SELECT * FROM " + strNewTable + " WHERE "
                    + strNewField + " IN (" + strInClause + ");";

                // TODO: Figure out the best time to handle the portion of the WHERE 
                // that impacts the tables mentioned in the join portion of the SQL.

                if (MainClass.bDebug)
                {
                    Console.WriteLine("Inner join: " + strInnerSelect + "\n\n");
                }

                SelectCommand selectCommand = new SelectCommand(_database);
                object objReturn = selectCommand.executeStatement(strInnerSelect);

                if (objReturn is DataTable)
                {
                    DataTable dtInnerJoinResult = (DataTable)objReturn;
                    dtReturn = InfrastructureUtils.equijoinTables(dtReturn, dtInnerJoinResult, strOldField, strNewField);
                }
                else
                {
                    throw new Exception("Illegal inner select: " + strInnerSelect);
                }
            }
            
            return dtReturn;
        }

        private DataTable _initDataTable(CommandParts selectParts)
        {
            DataTable dtReturn = new DataTable();
            dtReturn.TableName = selectParts.strTableName;

            // info on creating a datatable by hand here: 
            // http://msdn.microsoft.com/en-us/library/system.data.datacolumn.datatype.aspx
            foreach (Column colTemp in selectParts.acolInSelect)
            {
                string strColNameForDT = WhereProcessor.OperativeName(colTemp.strColName, selectParts.dictColToSelectMapping);
                // In case we fuzzy matched a name, take what was in the SELECT statement, if it was explicitly named.
                if (selectParts.dictColToSelectMapping.ContainsKey(colTemp.strColName))
                {
                    strColNameForDT = selectParts.dictColToSelectMapping[colTemp.strColName];
                }
                DataColumn colForDt = new DataColumn(strColNameForDT);
                //colForDt.MaxLength = colTemp.intColLength;    // MaxLength is only useful for string columns, strangely enough.

                // TODO: Be more deliberate about these mappings.
                // Right now, I'm not really worried about casting correctly, etc.
                // Probably grouping them poorly.
                switch (colTemp.colType)
                {
                    case COLUMN_TYPES.SINGLE_CHAR:
                    case COLUMN_TYPES.CHAR:
                        colForDt.DataType = System.Type.GetType("System.String");
                        colForDt.MaxLength = colTemp.intColLength;
                        break;

                    case COLUMN_TYPES.BYTE:
                    case COLUMN_TYPES.INT:
                    case COLUMN_TYPES.AUTOINCREMENT:
                        colForDt.DataType = System.Type.GetType("System.Int32");
                        break;

                    case COLUMN_TYPES.FLOAT:
                    case COLUMN_TYPES.DECIMAL:
                        colForDt.DataType = System.Type.GetType("System.Decimal");
                        break;

                    case COLUMN_TYPES.DATETIME:
                        colForDt.DataType = System.Type.GetType("System.DateTime");
                        break;

                    default:
                        throw new Exception("Unhandled column type in Select Command: " + colTemp.colType);
                }
                
                dtReturn.Columns.Add(colForDt);
            }

            return dtReturn;
        }
    }
}
