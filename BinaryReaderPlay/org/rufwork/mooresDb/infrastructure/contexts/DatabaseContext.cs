// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.IO;
using System.Collections.Generic;
using org.rufwork.mooresDb.exceptions;

namespace org.rufwork.mooresDb.infrastructure.contexts
{
    public class DatabaseContext
    {
        public StringComparison caseSetting = StringComparison.CurrentCultureIgnoreCase;    // TODO: Expose this more elegantly/deliberately.  Way too many local StringComparison.CurrentCultureIgnoreCase uses now.
        public string strDbLoc = "";
        private Dictionary<string, TableContext> _dictTables = new Dictionary<string, TableContext>();

        public DatabaseContext (string strDbLoc)
        {
            try
            {
                this.useNewDatabase(strDbLoc);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error initializing database: " + e.Message);
            }
        }

        /// <summary>
        /// Returns null if table doesn't exist in DatabaseContext.
        /// </summary>
        public TableContext getTableByName (string strTableName)
        {
            TableContext table = null;

            // Doing the weird foreach instead of ContainsKey so we can be case insensitive.
            foreach (string key in _dictTables.Keys)
            {
                if (key.Equals(strTableName, this.caseSetting))
                {
                    table = _dictTables[key];
                    break;
                }
            }

            return table;
        }

        public void addNewTable(TableContext tableNew)
        {
            _dictTables.Add(tableNew.strTableName, tableNew);
        }

        public void removeExistingTable(TableContext tableToDrop)
        {
            string strTableName = tableToDrop.strTableName;

            if (!_dictTables.Remove(strTableName))
            {
                throw new Exception("Unable to remove table: " + strTableName);
            }
        }

        public string tableCheck()  {
            string strReturn = "Tables in db" + System.Environment.NewLine 
                + "============" + System.Environment.NewLine
                + System.Environment.NewLine;

            foreach (KeyValuePair<string, TableContext> keyAndTable in _dictTables) {
                TableContext table = keyAndTable.Value;
                strReturn += table.strTableName + System.Environment.NewLine;
            }
            return strReturn;
        }

        // I'm sort of breaking away from looking at files (the "Truth") and
        // abstracting this in a way that seems unnecessary, but I'm not sure
        // that it's "wrong", per se.
        public bool doesTableExistInDatabase(string strTableName)   {
            return _dictTables.ContainsKey(strTableName);
        }

        public void useNewDatabase(string strDbLoc)
        {
            int i = -1;
            try {
                if (!Directory.Exists(strDbLoc))
                {
                    Directory.CreateDirectory(strDbLoc);
                }

                this.strDbLoc = strDbLoc;
                _dictTables = new Dictionary<string, TableContext>();   // blank whatever was in there before.

                string[] astrTableName = Directory.GetFiles(strDbLoc, "*.mdbf");
                for (i = 0; i < astrTableName.Length; i++)
                {
                    string strTableName = Path.GetFileNameWithoutExtension(astrTableName[i]);
                    try
                    {
                        _dictTables.Add(strTableName, new TableContext(strDbLoc, strTableName));
                    }
                    catch (ImproperDatabaseFileException eFile)
                    {
                        // quietly eat/log this one; we just don't have that table loaded.
                        Console.WriteLine("@@@@@  Bogus db file: " + eFile.Message);
                    }
                    // TODO: Consider continuing to parse out different exception types.
                    // and throwing if something happens here (uncaptured).
                    catch (Exception e)
                    {
                        Console.WriteLine("@@@@@  General catch for bogus db file: " + astrTableName[i] + System.Environment.NewLine
                            + "\t" + e.Message);
                    }

                }
            } catch (Exception ex) {
                _dictTables = new Dictionary<string, TableContext>();
                throw new Exception("Unable to load database. NOTE: No current database context: (" + i + ") " + ex.Message);
            }
        }
    }
}

