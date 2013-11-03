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
using org.rufwork.utils;


namespace org.rufwork.mooresDb.infrastructure.commands
{
    // convenience class to hold different parts of the SELECT
    // statement's text.
    class SelectParts
    {
        public string strSelect;
        public string strFrom;
        public string strWhere;
        public string strOrderBy;
        public string strInnerJoinKludge;

        public string strTableName; // TODO: Ob need to go to a collection of some sort

        public Column[] acolInSelect;
        public Dictionary<string, string> dictColToSelectMapping = new Dictionary<string, string>();
        private DatabaseContext _database;
        private TableContext _tableContext;

        public SelectParts(DatabaseContext database, string strSql)
        {
            _database = database;
            _parseStatement(strSql);
            _getColumnsToReturn();
        }

        public void _parseStatement(string strSql)
        {
            int intTail = strSql.Length;
            int intIndexOf = -1;

            if (!strSql.ToLower().StartsWith("select") || !strSql.ToLower().Contains("from"))
            {
                throw new Exception("Invalid SELECT statement");
            }

            intIndexOf = strSql.IndexOf("ORDER BY", StringComparison.CurrentCultureIgnoreCase);
            if (-1 < intIndexOf)
            {
                this.strOrderBy = strSql.Substring(intIndexOf, intTail - intIndexOf);
                intTail = intIndexOf;
            }

            intIndexOf = strSql.IndexOf("WHERE", StringComparison.CurrentCultureIgnoreCase);
            if (-1 < intIndexOf)
            {
                this.strWhere = strSql.Substring(intIndexOf, intTail - intIndexOf);
                intTail = intIndexOf;
            }

            intIndexOf = strSql.IndexOf("FROM", StringComparison.CurrentCultureIgnoreCase);
            if (-1 < intIndexOf)
            {
                this.strFrom = strSql.Substring(intIndexOf, intTail - intIndexOf);

                // Look for inner join.
                // TODO: Another reserved word that we don't really want a table to be named ("join").
                this.strInnerJoinKludge = "";
                if (this.strFrom.IndexOf(" join ", StringComparison.CurrentCultureIgnoreCase) > -1)
                {
                    int intInnerJoin = this.strFrom.IndexOf("inner join", StringComparison.CurrentCultureIgnoreCase);
                    if (intInnerJoin > -1)
                    {
                        this.strInnerJoinKludge = this.strFrom.Substring(intInnerJoin);
                        this.strFrom = this.strFrom.Substring(0, intInnerJoin);
                    }
                }

                this.strTableName = this.strFrom.Split()[1];
                _tableContext = _database.getTableByName(this.strTableName);
                if (null == _tableContext)
                {
                    throw new Exception("Table does not exist: " + this.strTableName);
                }
                intTail = intIndexOf;
            }

            this.strSelect = strSql.Substring(0, intTail); 
        }

        public void _getColumnsToReturn()
        {
            Queue<Column> qCol = new Queue<Column>();
            Column[] acolReturn = null;
            Column[] allColumns = _tableContext.getColumns();   // TODO: I think all you really want here is the length, right?
                // Makes sense to cache it, sure, but we're letting TableContext do most of the logic, so that's not really helpful.

            string[] astrCmdTokens = Utils.stringToNonWhitespaceTokens2(strSelect);
            if ("*" == astrCmdTokens[1])
            {
                acolReturn = _tableContext.getColumns();
            }
            else
            {
                for (int i = 1; i < astrCmdTokens.Length; i++)
                {
                    Column colTemp = _tableContext.getColumnByName(astrCmdTokens[i]);
                    if (null != colTemp)    {
                        qCol.Enqueue(colTemp);
                        if (!colTemp.strColName.Equals(astrCmdTokens[i], StringComparison.CurrentCultureIgnoreCase))
                        {
                            this.dictColToSelectMapping.Add(colTemp.strColName, astrCmdTokens[i]);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Did not find: " + astrCmdTokens[i]);
                        // I guess that should throw an exception.  You asked for a col that doesn't seem to exist.
                        throw new Exception("SELECT Column does not exist: " + astrCmdTokens[i]);
                    }
                }
                acolReturn = qCol.ToArray();
            }

            this.acolInSelect = acolReturn;
        }
    }

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
            SelectParts selectParts = new SelectParts(_database, strSql);

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
            List<Comparison>  lstWhereConditions = _createWhereConditions(selectParts.strWhere);
            _selectRows(ref dtReturn, selectParts.acolInSelect, lstWhereConditions, selectParts.dictColToSelectMapping);

            // To take account of joins, we basically need to create a SelectParts
            // per inner join.  So we need to create a WHERE from the table we 
            // just selected and then send those values down to a new _selectRows.
            if (selectParts.strInnerJoinKludge.Length > 0)
            {
                dtReturn = _processInnerJoin(dtReturn, selectParts.strInnerJoinKludge, selectParts.strTableName);
            }

            if (null != selectParts.strOrderBy && selectParts.strOrderBy.Length > 9)  {
                dtReturn.DefaultView.Sort = selectParts.strOrderBy.Substring(9);
                dtReturn = dtReturn.DefaultView.ToTable();
            }

            return dtReturn;
        }

        private List<Comparison> _createWhereConditions(string strWhere)
        {
            List<Comparison> lstReturn = new List<Comparison>();

            if (!string.IsNullOrWhiteSpace(strWhere)) {
                strWhere = strWhere.Substring(6);
                //string[] astrClauses = Regex.Split (strWhere, @"AND");
                string[] astrClauses = strWhere.Split(new string[] { "AND" }, StringSplitOptions.None);

                for (int i=0; i < astrClauses.Length; i++)    {
                    Comparison comparison = null;
                    string strClause = astrClauses[i].Trim();
                    if (MainClass.bDebug) Console.WriteLine("Where clause #" + i + " " + strClause);

                    if (strClause.ToUpper().Contains(" IN "))
                    {
                        CompoundComparison inClause = new CompoundComparison(GROUP_TYPE.OR);
                        if (MainClass.bDebug) Console.WriteLine("IN clause: " + strClause);
                        string strField = strClause.Substring(0, strClause.IndexOf(' '));

                        string strIn = strClause.Substring(strClause.IndexOf('(') + 1, strClause.LastIndexOf(')') - strClause.IndexOf('(') - 1);
                        string[] astrInVals = strIn.Split(',');
                        foreach (string strInVal in astrInVals)
                        {
                            string strFakeWhere = strField + " = " + strInVal;
                            inClause.lstComparisons.Add(_createComparison(strFakeWhere));
                        }
                        lstReturn.Add(inClause);
                    }
                    else
                    {
                        comparison = _createComparison(strClause);
                        if (null != comparison)
                        {
                            lstReturn.Add(comparison);
                        }
                        else
                        {
                            Console.WriteLine("Uncaptured WHERE clause type: " + strClause);
                        }
                    }
                }
            }

            return lstReturn;
        }

        private Comparison _createComparison(string strClause)
        {
            char chrOperator = '=';
            if (strClause.Contains('<'))
            {
                chrOperator = '<';
            }
            else if (strClause.Contains('>'))
            {
                chrOperator = '>';
            }
            else if (!strClause.Contains('='))
            {
                throw new Exception("Illegal comparison type in SelectCommand: " + strClause);
            }
            
            string[] astrEqualsParts = strClause.Split(chrOperator);
            Column colToConstrain = _table.getColumnByName(astrEqualsParts[0].Trim());
            if (null == colToConstrain)
            {
                throw new Exception("Column not found in SELECT statement: " + astrEqualsParts[0]);
            }

            BaseSerializer serializer = Router.routeMe(colToConstrain);
            byte[] abytComparisonVal = serializer.toByteArray(astrEqualsParts[1].Trim());

            return new Comparison(chrOperator, colToConstrain, abytComparisonVal);
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
            dictTables.Add(strParentTable, dtReturn);

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

                //Console.WriteLine("Phrase: " + strInnerJoin + System.Environment.NewLine 
                //    + "New table/field: " + strNewTable + "/" + strNewField + System.Environment.NewLine
                //    + "Old table/field: " + strOldTable + "/" + strOldField);
                string strInClause = "";

                foreach (DataRow row in dictTables[strOldTable].Rows)
                {
                    strInClause += row[strOldField].ToString() + ",";
                }
                strInClause = strInClause.Trim (',');

                string strInnerSelect = "SELECT * FROM " + strNewTable + " WHERE "
                    + strNewField + " IN (" + strInClause + ");";

                // TODO: Figure out the best time to handle the portion of the WHERE 
                // that impacts the join portion of the SQL.

                if (MainClass.bDebug)
                {
                    Console.WriteLine("Inner join: " + strInnerSelect + "\n\n");
                }

                SelectCommand selectCommand = new SelectCommand(_database);
                object objReturn = selectCommand.executeStatement(strInnerSelect);
                
                if (objReturn is DataTable)
                {
                    DataTable dtInnerJoinResult = (DataTable)objReturn;

                    // Merge the two tables (one the total of any previous joins, the other
                    // from the latest new statement) together.
                    TableContext tableNew = _database.getTableByName(strNewTable);
                    if (null == tableNew)
                    {
                        throw new Exception(strNewTable + " is in JOIN but does not exist in database: " + _database.strDbLoc);
                    }
                    dtReturn = InfrastructureUtils.equijoinTables(dtReturn, dtInnerJoinResult, _table, tableNew, strOldField, strNewField);
                }
                else
                {
                    throw new Exception("Illegal inner select: " + strInnerSelect);
                }
            }
            
            return dtReturn;
        }

        private DataTable _initDataTable(SelectParts selectParts)
        {
            DataTable dtReturn = new DataTable();
            dtReturn.TableName = selectParts.strTableName;

            // info on creating a datatable by hand here: 
            // http://msdn.microsoft.com/en-us/library/system.data.datacolumn.datatype.aspx
            foreach (Column colTemp in selectParts.acolInSelect)
            {
                string strColNameForDT = _operativeName(colTemp.strColName, selectParts.dictColToSelectMapping);
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

        private void _selectRows(ref DataTable dtWithCols, Column[] acolsInSelect, List<Comparison> lstWhereConditions, Dictionary<string, string> dictColNameMapping)
        {
            using (BinaryReader b = new BinaryReader(File.Open(_table.strTableFileLoc, FileMode.Open)))
            {
                int intRowCount = _table.intFileLength / _table.intRowLength;

                b.BaseStream.Seek(2 * _table.intRowLength, SeekOrigin.Begin);  // TODO: Code more defensively in case it's somehow not the right/minimum length

                for (int i = 2; i < intRowCount; i++)
                {
                    byte[] abytRow = b.ReadBytes(_table.intRowLength);
                    bool bKeepRow = true;

                    // Check and make sure this is an active row, and has 
                    // the standard row lead byte, 0x11.  If not, the row
                    // should not be read.
                    // I'm going to switch this to make it more defensive 
                    // and a little easier to follow.
                    switch (abytRow[0])
                    {
                        case 0x88:
                            // DELETED
                            bKeepRow = false;
                            break;

                        case 0x11:
                            // ACTIVE
                            // Find if the WHERE clause says to exclude this row.
                            foreach (Comparison comparison in lstWhereConditions)
                            {
                                // Temp skip INs
                                if (comparison is CompoundComparison)
                                {
                                    bool bInKeeper = false;
                                    // Could use a lot more indexed logic here, but that'll need to be
                                    // an extension to this package to keep this pretty simple.
                                    // For now, this is basically handling a group of WHERE... ORs.
                                    foreach (Comparison compInner in ((CompoundComparison)comparison).lstComparisons)
                                    {
                                        if (_comparisonEngine(compInner, abytRow))
                                        {
                                            bInKeeper = true;
                                            break;
                                        }
                                    }
                                    bKeepRow = bKeepRow && bInKeeper;
                                }
                                else
                                {
                                    bKeepRow = bKeepRow && _comparisonEngine(comparison, abytRow);
                                }
                            }
                            break;

                        default:
                            throw new Exception("Unexpected row state in SELECT: " + abytRow[0]);
                    }

                    if (bKeepRow)   {
                        DataRow row = dtWithCols.NewRow();

                        foreach (Column mCol in acolsInSelect)
                        {
                            byte[] abytCol = new byte[mCol.intColLength];
                            Array.Copy(abytRow, mCol.intColStart, abytCol, 0, mCol.intColLength);
                            //Console.WriteLine(System.Text.Encoding.Default.GetString(abytCol));
                            
                            // now translate/cast the value to the column in the row.
                            row[_operativeName(mCol.strColName, dictColNameMapping)] = Router.routeMe(mCol).toNative(abytCol);
                        }

                        dtWithCols.Rows.Add(row);
                    }
                }
            }
        }

        /// <summary>
        /// Takes in a row's worth of bytes in a byte array and sees
        /// if the row's proper value matches the active comparison.
        /// </summary>
        /// <returns></returns>
        private bool _comparisonEngine(Comparison comparison, byte[] abytRow)
        {
            byte[] abytRowValue = new byte[comparison.colRelated.intColLength];
            Array.Copy(abytRow, comparison.colRelated.intColStart, abytRowValue, 0, comparison.colRelated.intColLength);

            // TODO: This is ugly.  Having CompareByteArrays on the serializers is ungainly at best.  Just not sure where else to put it.
            COMPARISON_TYPE? valueRelationship = Router.routeMe(comparison.colRelated).CompareBytesToVal(abytRowValue, comparison.objParsedValue);

            if (null == valueRelationship)
            {
                throw new Exception("Invalid value comparison in SELECT");
            }

            return valueRelationship == comparison.comparisonType;
        }

        // This subs in the name used in the SELECT if it's a fuzzy matched column.
        // TODO: Looking it up with every row is pretty danged inefficient.
        private string _operativeName(string strColname, Dictionary<string, string> dictNameMapping)
        {
            string strReturn = strColname;
            if (dictNameMapping.ContainsKey(strColname))
            {
                strReturn = dictNameMapping[strColname];
            }
            return strReturn;
        }

        public static void Main(string[] args)
        {
            //string strSql = "SELECT ID, CITY, LAT_N FROM TableContextTest WHERE city = 'Chucktown'";
            //string strSql = "SELECT ID, CITY, LAT_N FROM TableContextTest WHERE city = 'Chucktown' AND LAT_N > 32";
            string strSql = @"SELECT ID, CITY, LAT_N 
                    FROM jive 
                    INNER JOIN joinTest
                    ON jive.Id = joinTest.CityId
                    inner join joinTest2
                    on joinTest.Id = joinTest2.joinId
                    WHERE city = 'Chucktown' AND LAT_N > 32";
            DatabaseContext database = new DatabaseContext(MainClass.cstrDbDir);

            SelectCommand selectCommand = new SelectCommand(database);
            DataTable dtOut = selectCommand.executeStatement(strSql);

            Console.WriteLine(InfrastructureUtils.dataTableToString(dtOut));

            Console.WriteLine("Return to quit.");
            Console.Read();

        }
    }
}
