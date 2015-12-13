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

using org.rufwork.extensions;

namespace org.rufwork.mooresDb.infrastructure.commands
{
    class CreateTableCommand
    {
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
        public object executeStatement(string strSql)
        {
            string strErr = "";

            Match createTableMatch = Regex.Match(strSql, @"^CREATE\s*TABLE\s*`?\w*`?\s*\(", RegexOptions.IgnoreCase);
            List<string> lstRawNames = new List<string>();

            if (createTableMatch.Success)
            {
                string strTableName = Regex.Replace(createTableMatch.Groups[0].Value, @"\r\n?|\n", ""); // remove newlines with http://stackoverflow.com/a/8196219/1028230
                strTableName = strTableName.Substring(0, strTableName.ToLower().IndexOf("("));
                strTableName = strTableName.Substring(strTableName.ToLower().IndexOf("table") + 5).Trim().Trim('`');

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
                        int intFieldLength = -1;

                        string strNextColumnInfo = astrSections[i].Trim();

                        // If we're defining a primary key, which we don't support (yet, if ever), skip the line.
                        // Else do the normal thing.
                        if (strNextColumnInfo.StartsWith("PRIMARY", StringComparison.CurrentCultureIgnoreCase))
                        {
                            SqlDbSharpLogger.LogMessage("Primary key creation is currently ignored: " + strNextColumnInfo, "CreateTable executeStatement");
                        }
                        else
                        {
                            string[] astrColInfo = strNextColumnInfo.StringToNonWhitespaceTokens2();

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
                                        SqlDbSharpLogger.LogMessage(j + " :: " + astrColInfo[j], "Create table execute statement");
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
                                            throw new Exception("Idiosyncratically, column lengths of [exactly] 4369 are not allowed. " + astrColInfo[1]);
                                        }
                                        intFieldLength = intLength;
                                    }
                                    else
                                    {
                                        // We're going to step up from defaulting to a length of 20 in each case to
                                        // defining a default length for each column data type.
                                        intFieldLength = -1;
                                    }
                                }

                                // every column declaration has already been checked to ensure it has at least two entries (checked above)
                                // TODO: Check for statements expecting the default length but with modifiers, like
                                // `id` INT NOT NULL AUTO_INCREMENT DEFAULT NULL,
                                // This is handled with AUTO_INCREMENT because NOT NULL is thrown out as if it had the length.
                                // Which is to say, NOT NULL isn't passed along as part of the modifier string.
                                strColName = astrColInfo[0].Trim('`');
                                string strModifier = null;
                                if (astrColInfo.Length > 3)
                                    strModifier = string.Join(" ", astrColInfo, 3, astrColInfo.Length - 3);

                                colType = InfrastructureUtils.colTypeFromString(astrColInfo[1], intFieldLength, strModifier);

                                if (null == colType)
                                {
                                    strErr += "Illegal/Supported column type: " + astrColInfo[1] + "\n";
                                }
                                else
                                {
                                    COLUMN_TYPES colTypeCleaned = (COLUMN_TYPES)colType;    // got to be a better way to launder a nullable.
                                    if (intFieldLength < 0)
                                    {
                                        intFieldLength = _getDefaultLengthForType(colTypeCleaned);
                                    }
                                    string strRawName = strColName.Length > intFieldLength ? strColName.Substring(0,intFieldLength) : strColName;
                                    IEnumerable<string> coveredNames = lstRawNames.Where(name => name.StartsWith(strRawName, StringComparison.CurrentCultureIgnoreCase)
                                        || strRawName.StartsWith(name, StringComparison.CurrentCultureIgnoreCase));
                                    if (coveredNames.Count() > 0)
                                    {
                                        throw new Exception(string.Format(@"Field names would ""cover"" each other: `{0}` and `{1}`", strRawName, coveredNames.First()));
                                    }
                                    lstRawNames.Add(strRawName);
                                    _createColumn(strColName, colTypeCleaned, intFieldLength);
                                }

                                if (!strErr.Equals(""))
                                {
                                    break;
                                }
                            }
                        }   // end check for unsupported directives (like defining a primary key)
                    }   // eo table column creation for loop

                    if (MainClass.bDebug)
                    {
                        string strDebug = string.Empty;
                        for (int j = 0; j < _lstByteDataTypeRow.Count; j++)
                        {
                            strDebug += "0x" + _lstByteDataTypeRow[j].ToString("X2") + ", \n";
                        }
                        strDebug += "\n\n";

                        for (int j = 0; j < _lstByteColNames.Count; j++)
                        {
                            strDebug += "0x" + _lstByteColNames[j].ToString("X2") + ", \n";
                        }
                        strDebug += "\n";
                        strDebug += _table.strTableFileLoc + "\n";

                        SqlDbSharpLogger.LogMessage(strDebug, "Create table execute statement");
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

            strErr = strErr.Equals("") ? "Table created successfully." : "Create table error" + System.Environment.NewLine + strErr;
            return strErr;
        }

        // A very tedious function.
        private int _getDefaultLengthForType(COLUMN_TYPES colType)
        {
            int intReturn = -1;
            switch (colType)
            {
                case COLUMN_TYPES.AUTOINCREMENT:
                    intReturn = 4;
                    break;

                case COLUMN_TYPES.BIT:
                    intReturn = 1;
                    break;

                case COLUMN_TYPES.CHAR:
                    intReturn = 20;
                    break;

                case COLUMN_TYPES.DATETIME:
                    intReturn = 8;
                    break;

                // currently treated the same.
                case COLUMN_TYPES.DECIMAL:
                case COLUMN_TYPES.FLOAT:
                    intReturn = 10;
                    break;

                case COLUMN_TYPES.INT:
                    intReturn = 4;  // 4*8 = Int32
                    break;

                case COLUMN_TYPES.TINYINT:
                    intReturn = 1;
                    break;

                case COLUMN_TYPES.SINGLE_CHAR:
                    intReturn = 1;
                    break;

                default:
                    throw new Exception("Column Type has no default length; please declare one: " + colType.ToString());

            }
            return intReturn;
        }

        private void _createColumn(string strName, COLUMN_TYPES colType, int intLength)
        {
            List<byte> lstBytToAdd = new List<byte>();
            lstBytToAdd.Add((byte)colType);

            if (Column.IsSingleByteType(colType))
            {
                if (intLength != 1)
                {
                    throw new Exception("cannot create column. Single byte column type with length greater than 1: " + strName + " :: " + intLength);    // TODO: Consider string prefixes for all exceptions in each class.
                }
                lstBytToAdd.Add(0x11);  // bookend with 11s
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
                    byte[] abyteIntLength = Utils.IntToByteArray(intLength);
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
