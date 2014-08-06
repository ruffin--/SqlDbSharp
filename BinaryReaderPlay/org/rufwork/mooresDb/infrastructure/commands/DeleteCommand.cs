// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

// TODO: There are (obviously) many places where this shares
// nearly or exactly identical code with SelectCommand.  We need to
// refactor that jive.

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


namespace org.rufwork.mooresDb.infrastructure.commands  {
    // convenience class to hold different parts of the SELECT
    // statement's text.
    class DeleteParts
    {
        public string strDelete;
        public string strFrom;
        public string strWhere;

        public string strTableName; // TODO: Ob need to go to a collection of some sort

        private DatabaseContext _database;
        private TableContext _tableContext;

        public DeleteParts(DatabaseContext database, string strSql)
        {
            _database = database;
            _parseStatement(strSql);
        }

        public void _parseStatement(string strSql)
        {
            int intTail = strSql.Length;
            int intIndexOf = -1;

            // TODO: Standardize on CurrentCultureIgnoreCase or ToLower.
            if (!strSql.ToLower().StartsWith("delete") || !strSql.ToLower().Contains("from"))
            {
                throw new Exception("Invalid DELETE statement");
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
                this.strTableName = this.strFrom.Split()[1];
                _tableContext = _database.getTableByName(this.strTableName);
                if (null == _tableContext)
                {
                    throw new Exception("Table does not exist: " + this.strTableName);
                }
                intTail = intIndexOf;
            }

            this.strDelete = strSql.Substring(0, intTail); 
        }
    }

    public class DeleteCommand
    {
        private TableContext _table;
        private DatabaseContext _database;

        public DeleteCommand (DatabaseContext database)
        {
            _database = database;
        }

        // TODO: Create Command interface
        public void executeStatement(string strSql)
        {
            DeleteParts deleteParts = new DeleteParts(_database, strSql);

            if (MainClass.bDebug)
            {
                Console.WriteLine("DELETE: " + deleteParts.strDelete);
                Console.WriteLine("FROM: " + deleteParts.strFrom);
                Console.WriteLine("WHERE: " + deleteParts.strWhere);
            }

            _table = _database.getTableByName(deleteParts.strTableName);

            List<Comparison> lstWhereConditions = _createWhereConditions(deleteParts.strWhere);
            _deleteRows(lstWhereConditions);
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

                    // TODO: Use real RegExes so we're not keying on non-actionable signs.
                    // TODO: Do the selecting in the binary, but sort with DataTable.
                    string patternHasEquals = "=";
                    Match equalsMatch = Regex.Match(strClause, patternHasEquals);
                    if (equalsMatch.Success)
                    {
                        string[] astrEqualsParts = Regex.Split(strClause, patternHasEquals);
                        if (MainClass.bDebug) Console.WriteLine("Equals clause: " + astrEqualsParts[0] + " :: " + astrEqualsParts[1]);

                        // TODO: Trim in regexp processing?
                        // TODO: Factor out split on token by doing all three in one regexp before hitting the if tree.
                        // Would remove colToConstrain, null check, and serializer construction from each.
                        Column colToConstrain = _table.getColumnByName(astrEqualsParts[0].Trim());

                        if (null == colToConstrain)
                        {
                            throw new Exception("Column not found in DELETE statement (0): " + astrEqualsParts[0]);
                        }

                        BaseSerializer serializer = Router.routeMe(colToConstrain);
                        byte[] abytComparisonVal = serializer.toByteArray(astrEqualsParts[1].Trim());

                        comparison = new Comparison(COMPARISON_TYPE.EQUALS, colToConstrain, abytComparisonVal);
                    }
                    else
                    {
                        string patternHasLt = "<";
                        Match ltMatch = Regex.Match(strClause, patternHasLt);
                        if (ltMatch.Success)
                        {
                            string[] astrLtParts = Regex.Split(strClause, patternHasLt);
                            if (MainClass.bDebug) Console.WriteLine("Less than clause: " + astrLtParts[0] + " :: " + astrLtParts[1]);

                            // TODO: Trim in regexp processing?
                            Column colToConstrain = _table.getColumnByName(astrLtParts[0].Trim());

                            if (null == colToConstrain)
                            {
                                throw new Exception("Column not found in DELETE statement (1): " + astrLtParts[0]);
                            }

                            BaseSerializer serializer = Router.routeMe(colToConstrain);
                            byte[] abytComparisonVal = serializer.toByteArray(astrLtParts[1].Trim());

                            comparison = new Comparison(COMPARISON_TYPE.LESS_THAN, colToConstrain, abytComparisonVal);
                        }
                        else
                        {
                            string patternHasGt = ">";
                            Match gtMatch = Regex.Match(strClause, patternHasGt);
                            if (gtMatch.Success)
                            {
                                string[] astrGtParts = Regex.Split(strClause, patternHasGt);
                                if (MainClass.bDebug) Console.WriteLine("Greater than clause: " + astrGtParts[0] + " :: " + astrGtParts[1]);

                                // TODO: Trim in regexp processing?
                                Column colToConstrain = _table.getColumnByName(astrGtParts[0].Trim());

                                if (null == colToConstrain)
                                {
                                    throw new Exception("Column not found in DELETE statement (3): " + astrGtParts[0]);
                                }

                                BaseSerializer serializer = Router.routeMe(colToConstrain);
                                byte[] abytComparisonVal = serializer.toByteArray(astrGtParts[1].Trim());

                                comparison = new Comparison(COMPARISON_TYPE.GREATER_THAN, colToConstrain, abytComparisonVal);
                            }
                        }
                    }

                    if (null != comparison)
                    {
                        lstReturn.Add(comparison);
                    }
                }
            }

            return lstReturn;
        }

        private void _deleteRows(List<Comparison> lstWhereConditions)
        {
            using (BinaryReader b = new BinaryReader(File.Open(_table.strTableFileLoc, FileMode.Open)))
            {
                int intRowCount = _table.intFileLength / _table.intRowLength;

                b.BaseStream.Seek(2 * _table.intRowLength, SeekOrigin.Begin);  // TODO: Code more defensively in case it's somehow not the right/minimum length
                
                for (int i = 2; i < intRowCount; i++)
                {
                    byte[] abytRow = b.ReadBytes(_table.intRowLength);
                    
                    bool bRowMatch = true;

                    switch (abytRow[0])
                    {
                        case 0x88:
                            // DELETED
                            bRowMatch = false;
                            break;

                        case 0x11:
                            // ACTIVE
                            // Find if the WHERE clause says to exclude this row.
                            foreach (Comparison comparison in lstWhereConditions)
                            {
                                //Column colForWhere = comparison.colRelated;  //_table.getColumnByName(comparison.strColumnName);
                                byte[] abytRowValue = new byte[comparison.colRelated.intColLength];
                                Array.Copy(abytRow, comparison.colRelated.intColStart, abytRowValue, 0, comparison.colRelated.intColLength);

                                // TODO: This is ugly.  Having CompareByteArrays on the serializers is ungainly at best.  Just not sure where else to put it.
                                COMPARISON_TYPE? valueRelationship = Router.routeMe(comparison.colRelated).CompareBytesToVal(abytRowValue, comparison.objParsedValue);

                                if (null == valueRelationship)
                                {
                                    throw new Exception("Invalid value comparison in DELETE");
                                }

                                bRowMatch = bRowMatch && valueRelationship == comparison.comparisonType;
                            }
                            break;

                        default:
                            throw new Exception("Unexpected row state in DELETE: " + abytRow[0]);
                    }

                    if (bRowMatch)   {
                        byte[] abytErase = new byte[_table.intRowLength];   // should be initialized to zeroes.
                        // at least to test, I'm going to write it all over with 0x88s.
                        for (int j = 0; j < _table.intRowLength; j++) { abytErase[j] = 0x88; }

                        // move pointer back to the first byte of this row.
                        b.BaseStream.Seek(-1 * _table.intRowLength, SeekOrigin.Current);
                        b.BaseStream.Write(abytErase, 0, abytErase.Length);
                    }
                }
            }
        }
    }
}