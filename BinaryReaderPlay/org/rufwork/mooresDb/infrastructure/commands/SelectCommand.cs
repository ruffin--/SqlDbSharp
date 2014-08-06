// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using org.rufwork.mooresDb.infrastructure.tableParts;
using org.rufwork.mooresDb.infrastructure.serializers;
using org.rufwork.mooresDb.infrastructure.contexts;
using org.rufwork.mooresDb.infrastructure;
using org.rufwork.mooresDb.infrastructure.commands.Processors;
using org.rufwork.utils;

using org.rufwork.extensions;

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
            Queue<TableContext> qAllTables = new Queue<TableContext>();
            qAllTables.Enqueue(_table);
            dtReturn = _initDataTable(selectParts);

            WhereProcessor.ProcessRows(ref dtReturn, _table, selectParts);

            // (Joins are only in selects, so this isn't part of WhereProcessing.)
            //
            // To take account of joins, we basically need to create a SelectParts
            // per inner join.  So we need to create a WHERE from the table we 
            // just selected and then send those values down to a new _selectRows.
            if (selectParts.strInnerJoinKludge.Length > 0)
            {
                
                dtReturn = _processInnerJoin(qAllTables, dtReturn, selectParts.strInnerJoinKludge, selectParts.strTableName, selectParts.strOrderBy);
            }

            if (null != selectParts.strOrderBy && selectParts.strOrderBy.Length > 9)
            {
                // ORDER BY needs to make sure it's not sorting on a fuzzy named column
                // that may not have been explicitly selected in the SELECT.
                string[] astrOrderByFields = selectParts.strOrderBy.Substring(9).Split(',');    // Substring(9) to get rid of "ORDER BY "
                string strCleanedOrderBy = string.Empty;

                foreach (string orderByClause in astrOrderByFields)
                {
                    bool ascNotDesc = true;

                    string strOrderByClause = orderByClause.Trim();
                    string strField = orderByClause.Trim();

                    if (strField.Split().Length > 1)
                    {
                        strField = strOrderByClause.Substring(0, strOrderByClause.IndexOf(' ')).Trim();
                        string strAscDesc = strOrderByClause.Substring(strOrderByClause.IndexOf(' ')).Trim();
                        ascNotDesc = (-1 == strAscDesc.IndexOf("DESC", StringComparison.CurrentCultureIgnoreCase));
                    }

                    strOrderByClause += ",";    // This is the default value if there's no fuzziness, and it needs the comma put back.

                    // TODO: Integrate fields prefixed by specific table names.
                    if (!dtReturn.Columns.Contains(strField))
                    {
                        // Check for fuzziness.
                        foreach (TableContext table in qAllTables)
                        {
                            if (!table.containsColumn(strField, false) && table.containsColumn(strField, true))
                            {
                                strOrderByClause = table.getRawColName(strField)
                                    + (ascNotDesc ? " ASC" : " DESC")
                                    + ",";
                                break;
                            }
                        }
                    }

                    strCleanedOrderBy += " " + strOrderByClause;
                }

                dtReturn.DefaultView.Sort = strCleanedOrderBy.Trim(',');
                dtReturn = dtReturn.DefaultView.ToTable();
            }

            return dtReturn;
        }

        private DataTable _processInnerJoin(Queue<TableContext> qAllTables, DataTable dtReturn, string strJoinText, string strParentTable, string strOrderBy)
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

                // Kludge alert -- must have table prefixes for now.
                if (!astrTokens[2].Contains(".") || !astrTokens[4].Contains("."))
                {
                    throw new Exception(string.Format(
                        "For now, joined fields must include table prefixes: {0} {1}",
                        astrTokens[2],
                        astrTokens[2])
                    );
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

                string strInClause = string.Empty;
                string strFuzzyJoinFields = string.Empty;

                TableContext tableOld = _database.getTableByName(strOldTable);
                TableContext tableNew = _database.getTableByName(strNewTable);
                qAllTables.Enqueue(tableNew);   // we need this to figure out column parents later.

                //==========================================================================
                // ADD COLUMNS TO TABLE WITH GIVEN NAMES
                //==========================================================================
                // Because this isn't convoluted enough, we've got to check and see if
                // the way the join field for the new table was written in the JOIN is
                // fuzzy or not, and if it is, we've got to add it to the stock subSELECT.
                // There are good ways to do this.  We're going for straightforward, but 
                // cache/efficiency-less.
                try
                {
                    if (!tableNew.containsColumn(strNewField, false))
                    {
                        strFuzzyJoinFields += "," + strNewField;
                    }
                }
                catch (Exception)
                {
                    throw new Exception("Inner join column name lookup didn't work: " + strNewTable + " :: " + strNewField);
                }

                // Now let's do something similar for fields in the ORDER BY clause, if
                // there are any, so that we can sort on fuzzy names.
                // TODO: Handle col names prefixed by table names.
                if (!string.IsNullOrWhiteSpace(strOrderBy))
                {
                    string[] astrOrderTokens = strOrderBy.StringToNonWhitespaceTokens2();
                    for (int i = 2; i < astrOrderTokens.Length; i++)
                    {
                        string testString = astrOrderTokens[i].Trim(' ', ',');
                        if (!tableNew.containsColumn(testString, false) && tableNew.containsColumn(testString, true))
                        {
                            strFuzzyJoinFields += "," + testString;
                        }
                    }
                }
                //==========================================================================
                //==========================================================================

                dictTables[strOldTable].CaseSensitive = false;  // TODO: If we keep this, do it in a smarter place.
                // Right now, strOldField has the name that's in the DataTable from the previous select.

                string strCanonicalOldField = tableOld.getRawColName(strOldField);    // allow fuzzy names in join, if not the DataTable -- yet.
                foreach (DataRow row in dictTables[strOldTable].Rows)
                {
                    strInClause += row[strCanonicalOldField].ToString() + ",";
                }
                strInClause = strInClause.Trim (',');

                string strInnerSelect = string.Format("SELECT *{0} FROM {1} WHERE {2} IN ({3});",
                    strFuzzyJoinFields,
                    strNewTable,
                    strNewField,
                    strInClause
                );

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
                    dtReturn = InfrastructureUtils.equijoinTables(
                        dtReturn,
                        dtInnerJoinResult,
                        tableOld.getRawColName(strOldField),
                        tableNew.getRawColName(strNewField)
                    );
                }
                else
                {
                    throw new Exception("Illegal inner select: " + strInnerSelect);
                }
            }
            
            return dtReturn;
        }

        // TODO: Remove concept of mainTable, and pass in an IEnumerable of all table
        // contexts so that we can figure out which owns each SELECTed column.
        private DataTable _initDataTable(CommandParts selectParts)
        {
            DataTable dtReturn = new DataTable();
            dtReturn.TableName = selectParts.strTableName;
            // So that I can have columns appear more than once in a single table,
            // I'm going to make a dupe of dictColToSelectMapping.  We'd have to go a
            // touch more complicated to keep the order from the original SELECT accurate.
            Dictionary<string, string> dictColMappingCopy = new Dictionary<string, string>(selectParts.dictFuzzyToColNameMappings);

            // info on creating a datatable by hand here: 
            // http://msdn.microsoft.com/en-us/library/system.data.datacolumn.datatype.aspx
            foreach (Column colTemp in selectParts.acolInSelect)
            {
                // "Translate" the SqlDBSharp column name to the name used in the SELECT statement.
                string strColNameForDT = WhereProcessor.GetFuzzyNameIfExists(colTemp.strColName, dictColMappingCopy);
                if (dictColMappingCopy.ContainsKey(strColNameForDT)) // these col names are from the SELECT statement, so they could be "fuzzy"
                {
                    dictColMappingCopy.Remove(strColNameForDT);  // This is the kludge that allows us to have the same col with different names.
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

                    case COLUMN_TYPES.AUTOINCREMENT:
                    case COLUMN_TYPES.TINYINT:
                    case COLUMN_TYPES.BIT:              // TODO: This will "work", but non 0/1 values can be inserted, obviously.  So it's a kludge for now.
                    case COLUMN_TYPES.INT:
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
