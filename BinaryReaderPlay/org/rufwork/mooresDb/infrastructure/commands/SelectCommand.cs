// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using org.rufwork.shims.data; // using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using org.rufwork.mooresDb.exceptions;
using org.rufwork.mooresDb.infrastructure.tableParts;
using org.rufwork.mooresDb.infrastructure.serializers;
using org.rufwork.mooresDb.infrastructure.contexts;
using org.rufwork.mooresDb.infrastructure;
using org.rufwork.mooresDb.infrastructure.commands.Processors;

using org.rufwork.utils;
using org.rufwork.extensions;
using org.rufwork.shims;
using org.rufwork.shims.data;

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
        public object executeStatement(string strSql)
        {
            object objReturn = null;
            DataTable dtReturn = new DataTable();

            // TODO: Think how to track multiple tables/TableContexts
            // Note that the constructor will set up the table named
            // in the SELECT statement in _table.
            CommandParts selectParts = new CommandParts(_database, _table, strSql, CommandParts.COMMAND_TYPES.SELECT);

            if (Globals.bDebug)
            {
                string strDebug = "SELECT: " + selectParts.strSelect + "\n";
                strDebug += "FROM: " + selectParts.strFrom + "\n";
                if (!string.IsNullOrEmpty(selectParts.strInnerJoinKludge))
                {
                    strDebug += "INNER JOIN: " + selectParts.strInnerJoinKludge + "\n";
                }
                strDebug += "WHERE: " + selectParts.strWhere + "\n";    // Note that WHEREs aren't applied to inner joined tables right now.
                strDebug += "ORDER BY: " + selectParts.strOrderBy + "\n";

                SqlDbSharpLogger.LogMessage(strDebug, "SelectCommand executeStatement");
            }

            _table = _database.getTableByName(selectParts.strTableName);
            Queue<TableContext> qAllTables = new Queue<TableContext>();
            qAllTables.Enqueue(_table);
            dtReturn = _initDataTable(selectParts);

            WhereProcessor.ProcessRows(ref dtReturn, _table, selectParts);

            //=====================================================================
            // POST-PROCESS INNER JOINS
            // (Joins are only in selects, so this isn't part of WhereProcessing.)
            //
            // To take account of joins, we basically need to create a SelectParts
            // per inner join.  So we need to create a WHERE from the table we 
            // just selected and then send those values down to a new _selectRows.
            //=====================================================================
            #region Post process inner joins
            if (selectParts.strInnerJoinKludge.Length > 0)
            {
                if (selectParts.qInnerJoinFields.Count < 1)
                {
                    selectParts.qInnerJoinFields.EnqueueIfNotContains("*");  // Kludge for "SELECT * FROM Table1 INNER JOIN..." or "SELECT test, * From...", etc
                }

                // TODO: Why aren't we just throwing in the whole selectParts again?
                dtReturn = _processInnerJoin(qAllTables, dtReturn, selectParts.strInnerJoinKludge, selectParts.strTableName, selectParts.strOrderBy, selectParts.qInnerJoinFields);

                // Now we need to make sure the order of the DataColumns reflects what we had
                // in the original SQL. At least initially, column order wasn't guaranteed in
                // _processInnerJoin, as it would add columns first for the "main" table and then
                // for each "inner SELECT".
                string strFromSelect = string.Join(", ", selectParts.qstrAllColumnNames.ToArray());
                string strInTable = string.Join(", ", dtReturn.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToArray());
                PCLConsole.WriteLine(string.Format(@"Select fields: {0}
Fields pushed into dtReturn: {1}", strFromSelect, strInTable));

                try
                {
                    string[] astrFromSelect = selectParts.qstrAllColumnNames.ToArray();
                    for (int i = 0; i < astrFromSelect.Length; i++)
                    {
                        dtReturn.Columns[astrFromSelect[i]].SetOrdinal(i);
                    }

                    // TODO: There are better ways to do this.
                    // TODO: Figure out if this handles all fuzzy name translations
                    // earlier in the SELECT process.
                    if (selectParts.lstrJoinONLYFields.Count() > 0)
                    {
                        foreach (string colName in selectParts.lstrJoinONLYFields)
                        {
                            dtReturn.Columns.Remove(colName);
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new SyntaxException("Problem reordering columns in inner join -- " + e.ToString());
                }
            }
            #endregion Post process inner joins
            //=====================================================================
            // EO POST-PROCESS INNER JOINS
            //=====================================================================


            // strOrderBy has had all whitespace shortened to one space, so we can get away with the hardcoded 9.
            if (null != selectParts.strOrderBy && selectParts.strOrderBy.Length > 9)
            {
                // ORDER BY needs to make sure it's not sorting on a fuzzy named column
                // that may not have been explicitly selected in the SELECT.
                string[] astrOrderByFields = selectParts.strOrderBy.Substring(9).Split(',');    // Substring(9) to get rid of "ORDER BY " <<< But, ultimately, why not tokenize here too?
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

            if (selectParts.dictRawNamesToASNames.Count > 0)
            {
                try
                {
                    foreach (KeyValuePair<string, string> kvp in selectParts.dictRawNamesToASNames)
                    {
                        dtReturn.Columns[kvp.Key].ColumnName = kvp.Value;
                    }
                }
                catch (Exception e)
                {
                    throw new SyntaxException("Illegal AS usage: " + e.ToString());
                }
                
            }

            if (selectParts.dictFnsAndFields.Count() > 0)
            {
                foreach (KeyValuePair<string, string> kvp in selectParts.dictFnsAndFields)
                {
                    switch (kvp.Key)
                    {
                        case "COUNT":
                            if (kvp.Value.Trim().Equals("*"))
                            {
                                objReturn = dtReturn.Rows.Count;
                            }
                            break;

                        default:
                            throw new SyntaxException("Unhandled function type: " + kvp.Key);
                    }
                }
            }

            // TODO: This is kind of cheesy and inefficient. Move real logic
            // that skips and takes without grabbing everything into the
            // WhereProcessor.
            // Note also that this works with the dictFnsAndFields stuff because
            // you shouldn't have LIMIT with MAX or COUNT.
            if (!String.IsNullOrWhiteSpace(selectParts.strLimit))
            {
                string strAfterLimit = selectParts.strLimit.Substring("LIMIT ".Length);
                string strTake = strAfterLimit;
                int intSkip = 0;
                int intTake;

                if (strAfterLimit.Contains(","))
                {
                    string[] astrSkipTake = strAfterLimit.Split(',');
                    if (!int.TryParse(astrSkipTake[0], out intSkip))
                        throw new Exception("Illegal LIMIT clause: " + selectParts.strLimit);
                    strTake = astrSkipTake[1];
                }

                if (int.TryParse(strTake, out intTake))
                    dtReturn = dtReturn.SkipTakeToTable(intSkip, intTake);
                else
                    throw new Exception("Illegal LIMIT clause: " + selectParts.strLimit);
            }

            objReturn = null == objReturn ? dtReturn : objReturn;
            return objReturn;
        }

        private DataTable _processInnerJoin(Queue<TableContext> qAllTables, DataTable dtReturn, string strJoinText,
            string strParentTable, string strOrderBy, Queue<string> qInnerJoinFields)
        {
            SqlDbSharpLogger.LogMessage("Note that WHERE clauses are not yet applied to JOINed tables.", "SelectCommnd _processInnerJoin");

            string strNewTable = null;
            string strNewField = null;
            string strOldField = null;
            string strOldTable = null;

            string strErrLoc = "init";

            Queue<string> qColsToSelectInNewTable = new Queue<string>();
            PCLConsole.WriteLine("join fields: " + string.Join("\n", qInnerJoinFields.ToArray()));

            try
            {
                strJoinText = System.Text.RegularExpressions.Regex.Replace(strJoinText, @"\s\n+", " ");
                string[] astrInnerJoins = strJoinText.ToLower().Split(new string[] { "inner join" }, StringSplitOptions.RemoveEmptyEntries);
                Dictionary<string, DataTable> dictTables = new Dictionary<string, DataTable>();
                dictTables.Add(strParentTable.ToLower(), dtReturn);

                strErrLoc = "starting inner join array";
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

                    strErrLoc = "determine old and new tables and fields";
                    string field1Parent = astrTokens[2].Substring(0, astrTokens[2].IndexOf("."));
                    string field2Parent = astrTokens[4].Substring(0, astrTokens[4].IndexOf("."));
                    string field1 = astrTokens[2].Substring(astrTokens[2].IndexOf(".") + 1);
                    string field2 = astrTokens[4].Substring(astrTokens[4].IndexOf(".") + 1);

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

                    Globals.logit(string.Format(@"old table: {0} 
old field: {1}
new table: {2}
new field: {3}",
                        strOldTable, strOldField, strNewTable, strNewField));

                    string strInClause = string.Empty;

                    TableContext tableOld = _database.getTableByName(strOldTable);
                    TableContext tableNew = _database.getTableByName(strNewTable);
                    qAllTables.Enqueue(tableNew);   // we need this to figure out column parents later.

                    // Now that we know the new table to add, we need to get a list of columns
                    // to select from it.
                    // Sources for these fields could be...
                    // 1.) The joining field.
                    // 2.) ORDER BY fields for the entire statement
                    // 3.) conventional SELECT fields.
                    //
                    // To prevent column name collision, let's go ahead and prefix them
                    // all with the table name.  A little unexpected, but a decent shortcut
                    // for now, I think.

                    strErrLoc = "beginning inner join select construction";

                    // 1.) Add the joining field.
                    qColsToSelectInNewTable.EnqueueIfNotContains(strNewField);

                    // 2.) ORDER BY fields that belong to this table.
                    if (!string.IsNullOrWhiteSpace(strOrderBy))
                    {
                        Globals.logit(strOrderBy);

                        strErrLoc = "constructing order by";
                        string[] astrOrderTokens = strOrderBy.StringToNonWhitespaceTokens2();
                        for (int i = 2; i < astrOrderTokens.Length; i++)
                        {
                            string strOrderField = astrOrderTokens[i].Trim(' ', ',');
                            string strOrderTable = strNewTable; // just to pretend. We'll skip it if the field doesn't exist here.  Course this means we might dupe some non-table prefixed fields.

                            if (strOrderField.Contains("."))
                            {
                                strOrderTable = strOrderField.Substring(0, strOrderField.IndexOf("."));
                                strOrderField = strOrderField.Substring(strOrderField.IndexOf(".") + 1);
                            }

                            if (strNewTable.Equals(strOrderTable, StringComparison.CurrentCultureIgnoreCase)
                                && !tableNew.containsColumn(strOrderField, false)
                                && tableNew.containsColumn(strOrderField, true))
                            {
                                qColsToSelectInNewTable.EnqueueIfNotContains(strNewTable + "." + strOrderField);
                            }
                        }
                    }

                    // 3.) Conventional SELECT fields
                    strErrLoc = "Creating select clause";
                    if (qInnerJoinFields.Count > 0)
                    {
                        if (qInnerJoinFields.Any(fld => fld.Equals(strNewTable + ".*", StringComparison.CurrentCultureIgnoreCase) || fld.Equals("*")))
                        {
                            qColsToSelectInNewTable.EnqueueIfNotContains("*");
                        }
                        else
                        {
                            foreach (string strTableDotCol in qInnerJoinFields)
                            {
                                string[] astrTableDotCol = strTableDotCol.Split('.');
                                if (strNewTable.Equals(astrTableDotCol[0], StringComparison.CurrentCultureIgnoreCase))
                                {
                                    Globals.logit("Adding field to join SELECT: " + astrTableDotCol[1].ScrubValue());
                                    qColsToSelectInNewTable.EnqueueIfNotContains(astrTableDotCol[1].ScrubValue()); // again, offensive parsing is the rule.
                                }
                            }
                        }
                    }

                    // TODO: Consider grabbing every column up front, perhaps, and
                    // then cutting out those columns that we didn't select -- but
                    // do SELECT fields as a post-processing task, rather than this
                    // inline parsing. Also allows us to bork on fields that aren't
                    // in tables a little easier; right now you could include bogus
                    // joined fields without error.

                    dictTables[strOldTable].CaseSensitive = false;  // TODO: If we keep this, do it in a smarter place.
                    // Right now, strOldField has the name that's in the DataTable from the previous select.

                    strErrLoc = @"Create WHERE IN clause for join ""inner"" SELECT";
                    // Now construct the `WHERE joinedField IN (X,Y,Z)` portion of the inner select
                    // we're about to fire off.
                    string strOperativeOldField = strOldField;
                    if (!dictTables[strOldTable].Columns.Contains(strOldField))
                    {
                        // Allow fuzzy names in join, if not the DataTable -- yet.
                        strOperativeOldField = tableOld.getRawColName(strOldField);
                    }

                    if (Globals.bDebug)
                    {
                        PCLConsole.WriteLine("Looking for " + strOperativeOldField + " in the columns");
                        foreach (DataColumn column in dictTables[strOldTable].Columns)
                        {
                            PCLConsole.WriteLine(column.ColumnName);
                        }
                    }

                    foreach (DataRow row in dictTables[strOldTable].Rows)
                    {
                        strInClause += row[strOperativeOldField].ToString() + ",";
                    }

                    strErrLoc = "Completing SELECT construction";
                    strInClause = strInClause.Trim(',');
                    if (string.IsNullOrEmpty(strInClause))
                    {
                        dtReturn = new DataTable();
                    }
                    else
                    {
                        Globals.logit("Columns in inner JOIN select: " + string.Join(", ", qColsToSelectInNewTable.ToArray()));
                        string strInnerSelect = string.Format("SELECT {0} FROM {1} WHERE {2} IN ({3});",
                            string.Join(",", qColsToSelectInNewTable.ToArray()),
                            strNewTable,
                            strNewField,
                            strInClause
                        );
                        qColsToSelectInNewTable = new Queue<string>();
                        strErrLoc = strInnerSelect;

                        // TODO: Figure out the best time to handle the portion of the WHERE 
                        // that impacts the tables mentioned in the join portion of the SQL.
                        // Note: I think now we treat it just like the ORDER BY.  Not that
                        // complicated to pull out table-specific WHERE fields and send along
                        // with the reconsitituted "inner" SQL statement.

                        Globals.logit("Inner join: " + strInnerSelect + "\n\n", "select command _processInnerJoin");

                        SelectCommand selectCommand = new SelectCommand(_database);
                        object objReturn = selectCommand.executeStatement(strInnerSelect);

                        if (objReturn is DataTable)
                        {
                            DataTable dtInnerJoinResult = (DataTable)objReturn;
                            dtReturn = InfrastructureUtils.equijoinTables(
                                dtReturn,
                                dtInnerJoinResult,
                                strOperativeOldField,
                                strNewField
                            );
                        }
                        else
                        {
                            strErrLoc = "Datatable not returned: " + strInnerSelect;
                            throw new SyntaxException("Illegal inner select: " + strInnerSelect);
                        }
                    }
                }

            }
            catch (Exception e)
            {
                if (e.GetType() == typeof(SyntaxException))
                {
                    throw e;
                }
                else
                {
                    throw new SyntaxException(string.Format(@"Uncaptured join syntax error -- {0}: 
{1} 
{2}", strErrLoc, strJoinText, e.ToString()));
                }
            }

            return dtReturn;
        }

        // TODO: Remove concept of mainTable, and pass in an IEnumerable of all table
        // contexts so that we can figure out which owns each SELECTed column.
        private DataTable _initDataTable(CommandParts selectParts)
        {
            DataTable dtReturn = new DataTable();
            dtReturn.TableName = selectParts.strTableName;  // TODO: This borks on JOINs, right? That is, you need to call this something else "X JOIN Y" or similar.
            // So that I can have columns appear more than once in a single table,
            // I'm going to make a dupe of dictColToSelectMapping.  We'd have to go a
            // touch more complicated to keep the order from the original SELECT accurate.
            Dictionary<string, string> dictColMappingCopy = new Dictionary<string, string>(selectParts.dictFuzzyToColNameMappings);

            // info on creating a datatable by hand here: 
            // http://msdn.microsoft.com/en-us/library/system.data.datacolumn.datatype.aspx
            // TODO: Distinct() is probably/should be overkill.
            foreach (Column colTemp in selectParts.acolInSelect.Distinct())
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
