// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;

using System.Collections.Generic;
using System.Text;

using org.rufwork.mooresDb.infrastructure.tableParts;
using org.rufwork.mooresDb.infrastructure.serializers;
using org.rufwork.mooresDb.infrastructure.contexts;

namespace org.rufwork.mooresDb.infrastructure.commands
{
    public class InsertCommand
    {
        public byte[][] abytInsertOrUpdate;
        private TableContext _table;
        private DatabaseContext _database;
        
        public InsertCommand (DatabaseContext database)
        {
            _database = database;
        }
        
        /// <summary>
        /// TODO: Create an InsertCommandException type that takes msg, table, and field name.
        /// </summary>
        /// <param name="astrCmdTokens">string[] of tokens from the SQL, split on whitespace</param>
        /// <returns></returns>
        public bool executeInsert (string strSql)
        {
            byte[] abytFullRow = null;
            string[] astrCmdTokens = Utils.SqlToTokens(strSql);

            // TODO: Add a check for more than one row in the INSERT, which we don't support right now.
            // Less than 6 means we can't pull off an insert
            // INSERT INTO Table (Column) VALUES (value) 
            bool bQuickTokenCheck = !(astrCmdTokens.Length < 6 && astrCmdTokens[0].ToLower() != "insert" && astrCmdTokens[1].ToLower() != "into");
            bool bTextCheck = strSql.ToLower().Contains("values");  // TODO: Keep track of a lowercase version of the string to eliminate all this ToLower stuff?
            if (!bTextCheck)    {
                throw new Exception ("INSERT statement requires VALUES");   // TODO: again, make these all individual exception types.
            }   else if (!bQuickTokenCheck)    {
                throw new Exception("Illegal insert command -- Does not include INSERT or INTO at all or in the expected order or is too short.");
            }   else   {
                List<string> lstColumnNames = new List<string>();
                List<string> lstStringRowValues = new List<string>();
                Dictionary<string, byte[]> dictValsToWriteByColName = new Dictionary<string, byte[]>();   // just one row with insert right now.
                string strTableName;

                string strTemp;
                strTableName = astrCmdTokens[2];

                _table = _database.getTableByName(strTableName);
                if (null == _table)
                {
                    throw new Exception("Table not found in database: " + strTableName);
                }

                int i=3;
                strTemp = astrCmdTokens[i].Trim();
                while (!strTemp.Equals("values", StringComparison.CurrentCultureIgnoreCase) && i < astrCmdTokens.Length-1)
                {
                    lstColumnNames.Add (strTemp);
                    strTemp = astrCmdTokens[++i].Trim();
                }

                if (strTemp.ToLower() != "values")
                {
                    throw new Exception ("Illegal insert command 21");
                }
                else
                {
                    // okay, odd place for an else, I know, since the Exception would kill the if block anyhow.
                    while (strTemp.IndexOf(";") == -1 && i < astrCmdTokens.Length-1)
                    {
                        strTemp = astrCmdTokens[++i].Trim();
                        lstStringRowValues.Add(strTemp);  // I don't think we care where the ")" appears, do we?  Maybe I should split on parens first.  But INSERT doesn't have something after the ), right?
                    }
                }

                // can't tell if I'd rather keep this all in the else or pretend like these are
                // separate bits of logic.
                if (lstStringRowValues.Count != lstColumnNames.Count)    {
                    throw new Exception("Number of insert command columns and number of values are different; cannot insert row.");
                }   else {
                    if (MainClass.bDebug)
                    {
                        for (int j = 0; j < lstStringRowValues.Count; j++)
                        {
                            Console.WriteLine(lstColumnNames[j] + " :: " + lstStringRowValues[j]);
                        }
                    }

                    for (int m=0; m<lstColumnNames.Count; m++) {
                        string strColName = lstColumnNames[m];
                        Column colFound = _table.getColumnByName(strColName);

                        if (null != colFound)
                        {

                            if (COLUMN_TYPES.AUTOINCREMENT == colFound.colType)
                            {
                                throw new Exception("Cannot insert a value into an autoincrement field: " + colFound.strColName);
                            }

                            byte[] abytVal = null; // "raw" value.  Might not be the full column length.

                            BaseSerializer serializer = Router.routeMe(colFound);
                            abytVal = serializer.toByteArray(lstStringRowValues[m]);

                            // double check that the serializer at least
                            // gave you a value that's the right length so
                            // that everything doesn't go to heck (moved where 
                            // that was previously checked into the serializers)
                            if (abytVal.Length != colFound.intColLength)    {
                                throw new Exception("Improperly lengthed field from serializer: " + colFound.strColName + " :: "  + lstColumnNames[m]);
                            }

                            dictValsToWriteByColName.Add(colFound.strColName, abytVal);  // we'll put them in order with empty cols that weren't in the insert once we're done.
                        }
                        else    // else the column name wasn't in this table.  BORKED!
                        {
                            throw new Exception(strColName + " is not a valid column for " + strTableName + "; invalid INSERT.");
                        }
                    }
                }

                // once you have all the fields in the insert AND the table name, you have to reconcile 
                // against what columns have been left out to insert empty bytes for those.
                // we'll cheat and do that by trolling through the _tblMgr.Columns and match up
                // with those in the dictionary to create a row.
                // note that we've already matched all the cols in the dictValsToWrite... with the
                // colFound jive, above.  We don't need to recheck that they're legit here.
                Column[] allColumns = _table.getColumns();
                abytFullRow = new byte[_table.intRowLength];
                abytFullRow[0] = 0x11;
                int intByteCounter = 1; // 1 b/c we inserted 0x11 in byte 0.
                foreach (Column column in allColumns) 
                {
                    // So we already have a byte array, length matching the column's, full of 0x00
                    // (as that's a byte's default value in C#) in abytFullRow.  That value only
                    // changes if we're got something to insert.  We're "laying in" ranges of bytes
                    // like bricks into empty mortar when they exist.
                    if (dictValsToWriteByColName.ContainsKey(column.strColName))    {
                        byte[] abytColValue = dictValsToWriteByColName[column.strColName];

                        // keep in mind that column.intColLength should always match abytColValue.Length.  While I'm
                        // testing, I'm going to put in this check, but at some point, you should be confident enough
                        // to consider removing this check.
                        if (abytColValue.Length != column.intColLength) {
                            throw new Exception("Surprising value and column length mismatch");
                        }
                        // Copy in value over our mortar of 0x00s.
                        Buffer.BlockCopy(abytColValue, 0, abytFullRow, intByteCounter, abytColValue.Length);
                    }
                    else if (COLUMN_TYPES.AUTOINCREMENT == column.colType)
                    {
                        if (column.intAutoIncrementCount >= 16777216)
                        {
                            throw new Exception("Autoincrement overflow.  Congratulations.  Column: " + column.strColName);
                        }
                        column.intAutoIncrementCount++;
                        byte[] abytAutoIncrementValue = Utils.intToByteArray(column.intAutoIncrementCount, 4);  // NOTE: Changing from hard-coded 4 for AUTOINCREMENT length borks this
                        // This is the nasty bit.  We need to increase the spot where we keep
                        // the greatest autoincrement value so that, in case we delete, we can
                        // still pick up where we left off.  That is, because we increased the 
                        // "column length", we have to serialize that change to the underlying file.
                        _table.updateAutoIncrementCount(column.strColName, column.intAutoIncrementCount);          // Serialize the update to the autoincrement position.
                        Buffer.BlockCopy(abytAutoIncrementValue, 0, abytFullRow, intByteCounter, abytAutoIncrementValue.Length);
                    }

                    intByteCounter += column.intColLength;  // keep track of how many bytes into the full row we've handled.
                    // insert the end of column 0x11 and increment the byte counter.
                    System.Buffer.BlockCopy(new byte[] { 0x11 }, 0, abytFullRow, intByteCounter++, 1);

                    // Note that we count it off whether we inserted something (the INSERT included this column or it was an AUTO_INCREMENT) or it didn't (we keep values all 0x00).
                }
            }
            abytFullRow[abytFullRow.Length - 1] = 0x11; // insert final 0x11 to end the row
            _table.writeRow(abytFullRow);

            return true;
        }

        public static void Main(string[] args)
        {
            DatabaseContext database = new DatabaseContext(MainClass.cstrDbDir);
            string strSql = @"INSERT INTO TableContextTest (ID, CITY, STATE, LAT_N, LONG_W,)
                                VALUES (1, 'Chucktown', 'SC', 32.776, 79.931);";

            InsertCommand _insertCommand = new InsertCommand(database);
            _insertCommand.executeInsert(strSql);
            
            Console.WriteLine("Return to quit.");
            Console.Read();
        }
    }
}