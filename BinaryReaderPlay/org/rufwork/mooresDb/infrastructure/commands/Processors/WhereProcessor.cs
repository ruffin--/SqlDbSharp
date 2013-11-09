using org.rufwork.mooresDb.infrastructure;
using org.rufwork.mooresDb.infrastructure.contexts;
using org.rufwork.mooresDb.infrastructure.serializers;
using org.rufwork.mooresDb.infrastructure.tableParts;
using org.rufwork.utils;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

namespace org.rufwork.mooresDb.infrastructure.commands.Processors
{
    class WhereProcessor
    {
        public delegate object RowProcessor(byte[] abytRow, Column[] acolsInSelect, Dictionary<string, string> dictColNameMapping, ref DataTable table);

        public static object selectRowProcessor(byte[] abytRow, Column[] acolsInSelect, Dictionary<string,string> dictColNameMapping, ref DataTable table)
        {
            DataRow row = table.NewRow();
            foreach (Column mCol in acolsInSelect)
            {
                byte[] abytCol = new byte[mCol.intColLength];
                Array.Copy(abytRow, mCol.intColStart, abytCol, 0, mCol.intColLength);
                //Console.WriteLine(System.Text.Encoding.Default.GetString(abytCol));

                // now translate/cast the value to the column in the row.
                row[OperativeName(mCol.strColName, dictColNameMapping)] = Router.routeMe(mCol).toNative(abytCol);
            }
            table.Rows.Add(row);
            return true;
        }


        public static void ProcessRows(ref DataTable dtWithCols,
            TableContext table,
            SelectParts selectParts,
            RowProcessor fnRowProcessor)
        {
            Column[] acolsInSelect = selectParts.acolInSelect;
            string strWhere = selectParts.strWhere;
            Dictionary<string, string> dictColNameMapping = selectParts.dictColToSelectMapping;

            List<Comparison> lstWhereConditions = _CreateWhereConditions(strWhere, table);

            using (BinaryReader b = new BinaryReader(File.Open(table.strTableFileLoc, FileMode.Open)))
            {
                int intRowCount = table.intFileLength / table.intRowLength;
                b.BaseStream.Seek(2 * table.intRowLength, SeekOrigin.Begin);  // TODO: Code more defensively in case it's somehow not the right/minimum length

                for (int i = 2; i < intRowCount; i++)
                {
                    byte[] abytRow = b.ReadBytes(table.intRowLength);
                    bool bMatchingRow = true;

                    // Check and make sure this is an active row, and has 
                    // the standard row lead byte, 0x11.  If not, the row
                    // should not be read.
                    // I'm going to switch this to make it more defensive 
                    // and a little easier to follow.
                    switch (abytRow[0])
                    {
                        case 0x88:
                            // DELETED
                            bMatchingRow = false;
                            break;

                        case 0x11:
                            // ACTIVE
                            // Find if the WHERE clause says to exclude this row.
                            foreach (Comparison comparison in lstWhereConditions)
                            {
                                // For now, we're (somewhat clumsily) processing INs as lots of small ORs.
                                // And no, we're not actually supporting the OR statement in a regular WHERE yet.
                                if (comparison is CompoundComparison)
                                {
                                    bool bInKeeper = false;
                                    // Could use a lot more indexed logic here, but that'll need to be
                                    // an extension to this package to keep the logic simple.
                                    // This is a painful, bullheaded Moore's comparison.
                                    foreach (Comparison compInner in ((CompoundComparison)comparison).lstComparisons)
                                    {
                                        if (_ComparisonEngine(compInner, abytRow))
                                        {
                                            bInKeeper = true;
                                            break;
                                        }
                                    }
                                    bMatchingRow = bMatchingRow && bInKeeper;
                                }
                                else
                                {
                                    bMatchingRow = bMatchingRow && _ComparisonEngine(comparison, abytRow);
                                }
                            }
                            break;

                        default:
                            throw new Exception("Unexpected row state in SELECT: " + abytRow[0]);
                    }

                    if (bMatchingRow)
                    {
                        fnRowProcessor(abytRow, acolsInSelect, dictColNameMapping, ref dtWithCols);
                    }
                }
            }
            // nothing to return -- dt was passed by ref.
        }

        // This subs in the name used in the SELECT if it's a fuzzy matched column.
        // TODO: Seems like this might belong on the TableContext?
        // TODO: Looking it up with every row is pretty danged inefficient.
        public static string OperativeName(string strColname, Dictionary<string, string> dictNameMapping)
        {
            string strReturn = strColname;
            if (dictNameMapping.ContainsKey(strColname))
            {
                strReturn = dictNameMapping[strColname];
            }
            return strReturn;
        }

        /// <summary>
        /// Takes in a row's worth of bytes in a byte array and sees
        /// if the row's proper value matches the active comparison.
        /// </summary>
        /// <returns></returns>
        private static bool _ComparisonEngine(Comparison comparison, byte[] abytRow)
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

#region whereToComparisons
        private static List<Comparison> _CreateWhereConditions(string strWhere, TableContext table)
        {
            List<Comparison> lstReturn = new List<Comparison>();

            if (!string.IsNullOrWhiteSpace(strWhere))
            {
                strWhere = strWhere.Substring(6);
                string[] astrClauses = strWhere.Split(new string[] { "AND", "and" }, StringSplitOptions.None);

                for (int i = 0; i < astrClauses.Length; i++)
                {
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
                            inClause.lstComparisons.Add(WhereProcessor._CreateComparison(strFakeWhere, table));
                        }
                        lstReturn.Add(inClause);
                    }
                    else
                    {
                        comparison = WhereProcessor._CreateComparison(strClause, table);
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

        private static Comparison _CreateComparison(string strClause, TableContext table)
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

            string[] astrComparisonParts = strClause.Split(chrOperator);
            Column colToConstrain = table.getColumnByName(astrComparisonParts[0].Trim());
            if (null == colToConstrain)
            {
                throw new Exception("Column not found in SELECT statement: " + astrComparisonParts[0]);
            }

            BaseSerializer serializer = Router.routeMe(colToConstrain);
            byte[] abytComparisonVal = serializer.toByteArray(astrComparisonParts[1].Trim());

            return new Comparison(chrOperator, colToConstrain, abytComparisonVal);
        }
#endregion whereToComparisons
    }
}
