// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Text.RegularExpressions;
using System.IO;

using org.rufwork.mooresDb.infrastructure.tableParts;
using org.rufwork.mooresDb.infrastructure.contexts;
using org.rufwork.mooresDb.exceptions;

namespace org.rufwork.mooresDb.infrastructure.commands
{
    class CreateTableCommand
    {
        const int cintDefaultColumnLength = 20;   // should at least be larger than one
        private DatabaseContext _database;
        private TableContext _table;
        private string _strDbLoc;
        private List<byte> _lstByteDataTypeRow = new List<byte>();
        private List<byte> _lstByteColNames = new List<byte>();

        public CreateTableCommand (DatabaseContext database)
        {
            _database = database;
            _strDbLoc = _database.strDbLoc;
        }

        /// <summary>
        /// Processes a CREATE TABLE statement.  
        /// Throws any Exception on any failure.
        /// </summary>
        /// <param name="strSql"></param>
        public void executeStatement(string strSql)
        {
            string strErr = "";

            Match createTableMatch = Regex.Match(strSql, @"^CREATE\s*TABLE\s*\w*\s*\(", RegexOptions.IgnoreCase);

            if (createTableMatch.Success)
            {
                string strTableName = Regex.Replace(createTableMatch.Groups[0].Value, @"\r\n?|\n", ""); // remove newlines with http://stackoverflow.com/a/8196219/1028230
                strTableName = strTableName.Substring(0, strTableName.ToLower().IndexOf("("));
                strTableName = strTableName.Substring(strTableName.ToLower().IndexOf("table") + 5).Trim();

                if (null != _database.getTableByName(strTableName)) {
                    strErr += "Table " + strTableName + " already exists.\n";
                }
                else
                {
                    // Else this IS a legitimate location for a table file.  Begin.
                    // Store table loc in TableContext
                    _table = new TableContext();

                    // initialize rows with the 11 byte.
                    _lstByteDataTypeRow.Add(0x11);
                    _lstByteColNames.Add(0x11);

                    string strColInfo = strSql.Substring(strSql.IndexOf("(")+1);         // get rid of everything up until the first open parens.
                    strColInfo = strColInfo.Substring(0, strColInfo.LastIndexOf(")"));   // peel off the last closed parens.

                    string[] astrSections = Regex.Split(strColInfo, ",");

                    for (int i = 0; i < astrSections.Length; i++)
                    {
                        COLUMN_TYPES? colType = null;  // This really should never be null after running through the code.  It'll throw an exception first.
                        string strColName = "";
                        int intFieldLength = cintDefaultColumnLength;

                        string strNextColumnInfo = astrSections[i].Trim();
                        string[] astrColInfo = Utils.stringToNonWhitespaceTokens2(strNextColumnInfo);

                        if (astrColInfo.Length < 2) {
                            strErr += "Illegal column defintion; table not created: " + string.Join(":",astrColInfo) + "#\n";
                        }   else    {
                            //=====================
                            //======= DEBUG =======
                            //=====================
                            if (MainClass.bDebug)
                            {
                                for (int j = 0; j < astrColInfo.Length; j++)
                                {
                                    Console.WriteLine(j + " :: " + astrColInfo[j]);
                                }
                            }
                            //======================
                            //======================
                            if (3 <= astrColInfo.Length)
                            {
                                int intLength;
                                if (int.TryParse(astrColInfo[2], out intLength))
                                {
                                    if (4369 == intLength)
                                    {
                                        throw new Exception("idiosyncratically, column lengths of [exactly] 4369 are not allowed. " + astrColInfo[1]);
                                    }
                                    intFieldLength = intLength;
                                }
                            }

                            // every column declaration has already been checked to ensure it has at least two entries (checked above)
                            strColName = astrColInfo[0];
                            string strModifier = astrColInfo.Length > 3 ? astrColInfo[3] : null;
                            colType = InfrastructureUtils.colTypeFromString(astrColInfo[1], 1 == intFieldLength, strModifier);

                            if (null == colType)
                            {
                                strErr += "Illegal/Supported column type: " + astrColInfo[1] + "\n";
                            }
                            else
                            {
                                COLUMN_TYPES colTypeCleaned = (COLUMN_TYPES)colType;    // got to be a better way to launder a nullable.
                                _createColumn(strColName, colTypeCleaned, intFieldLength);
                            }

                            if (!strErr.Equals(""))
                            {
                                break;
                            }
                        }
                    }   // eo table column creation for loop

                    if (MainClass.bDebug)
                    {
                        for (int j = 0; j < _lstByteDataTypeRow.Count; j++)
                        {
                            Console.Write("0x" + _lstByteDataTypeRow[j].ToString("X2") + ", ");
                        }
                        Console.WriteLine();
                        Console.WriteLine();
                        for (int j = 0; j < _lstByteColNames.Count; j++)
                        {
                            Console.Write("0x" + _lstByteColNames[j].ToString("X2") + ", ");
                        }
                        Console.WriteLine();

                        Console.WriteLine(_table.strTableFileLoc);
                    }

                    // TODO: Instead of writing bytes here, I should probably create a list of column objects,
                    // with name, length, and COLUMN_TYPE for each, then let the table manager worry about
                    // bytes and specific implmentations.
                    _table.writeMetadataRowsAndPrepareNewTable(_lstByteDataTypeRow,_lstByteColNames, strTableName, _database.strDbLoc);
                    _database.addNewTable(_table);
                    
                }   // eo table exists check.
            }   // eo createTableMatch.Success regex check
            else
            {
                strErr += "SYNTAX ERROR: Illegal Create Table Statement" + System.Environment.NewLine;
                // go ahead and throw specific error type.
                throw new SyntaxException(strErr);
            }

            if (!strErr.Equals(""))
            {
                throw new Exception("Create table error" + System.Environment.NewLine
                    + strErr);
            }
        }

        private void _createColumn(string strName, COLUMN_TYPES colType, int intLength)
        {
            List<byte> lstBytToAdd = new List<byte>();
            lstBytToAdd.Add((byte)colType);

            if (colType == COLUMN_TYPES.SINGLE_CHAR || colType == COLUMN_TYPES.BYTE)
            {
                if (intLength != 1)
                {
                    throw new Exception("Bytes and single character columns must have length of 1");    // TODO: Consider string prefixes for all exceptions in each class.
                }
                // Otherwise, that's it.  We've got the length implicitly in the colType we wrote, above.
                // (and there's no space to add a length; the column only has one byte of metadata that 
                // we wrote in with .Add((byte)colType), above.
            }
            else if (COLUMN_TYPES.INT == colType && intLength != 4)
            {
                throw new Exception("For now, INTs must be 4 bytes in length.");
            }
            else if (COLUMN_TYPES.AUTOINCREMENT == colType && 4 != intLength)
            {
                throw new Exception("For now, autoincrement columns must be four byte integers.");
            }
            else
            {
                if (COLUMN_TYPES.AUTOINCREMENT == colType)
                {
                    // Keeping the length constant makes the rest of this setup trivial.
                    lstBytToAdd.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x11 });
                }
                else
                {
                    // Else determine and write the column's metadata.
                    byte[] abyteIntLength = Utils.intToByteArray(intLength);
                    int i = 0;

                    int intPaddingLength = intLength - ((abyteIntLength.Length - i) + 1);
                    if (MainClass.bDebug)
                    {
                        Console.WriteLine("intLength: " + intLength + " i: " + i
                            + " bytes for length: " + (abyteIntLength.Length - i)
                            + " intPaddingLength: " + intPaddingLength);
                    }

                    while (i < abyteIntLength.Length)
                    {
                        //Console.WriteLine("adding: " + abyteInt[i].ToString("X")); 
                        lstBytToAdd.Add(abyteIntLength[i++]);
                    }

                    // now pad out the space and put an 11 on the end.
                    while (intPaddingLength > 0)
                    {
                        lstBytToAdd.Add(0x00);
                        intPaddingLength--;
                    }
                    lstBytToAdd.Add(0x11);  // bookend with 11s
                }

                _lstByteDataTypeRow = _lstByteDataTypeRow.Concat(lstBytToAdd).ToList();


                //================================================================
                // Column names row
                //================================================================
                // Write out row of column names.  Watch out for insane kludges.
                // TODO: Look out for duplicate column names and for masking longer 
                // names when shortened.
                if (intLength > strName.Length)
                {
                    foreach (byte bytTemp in strName.ToCharArray())
                    {
                        _lstByteColNames.Add(bytTemp);
                    }
                    for (int j = strName.Length; j < intLength; j++)
                    {
                        _lstByteColNames.Add(0x00);
                    }
                }
                else
                {
                    string strShortenedName = strName.Substring(0, intLength);
                    Console.WriteLine("TODO: Write out a file with shortened name mappings just to make things easier.");
                    foreach (byte bytTemp in strShortenedName.ToCharArray())
                    {
                        _lstByteColNames.Add(bytTemp);
                    }
                }
                _lstByteColNames.Add(0x11);
            }
        }
    }
}
