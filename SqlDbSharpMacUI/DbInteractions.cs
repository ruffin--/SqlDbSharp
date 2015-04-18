// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;

using org.rufwork;
using org.rufwork.mooresDb.infrastructure.contexts;
using org.rufwork.mooresDb.infrastructure;
using org.rufwork.extensions;

namespace SqlDbSharpMacUI
{
    public class DbInteractions
    {
        DatabaseContext database = null;
        string strParentDir = string.Empty;
        string strTestTableName = "jive";

        public DbInteractions (string strParentDir)
        {
            this.strParentDir = strParentDir;
            this.database = new DatabaseContext(strParentDir);
        }

        public string processCommand(string strCmd, int intLineLength = -1)
        {
            string strReturn = string.Empty;
            Queue<string> qCmds = strCmd.Trim(' ').Trim('.').SplitSeeingSingleQuotesAndBackticks(";", true);

            foreach (string strSingleCommand in qCmds)
            {
                strCmd = strSingleCommand.Substring(0, strSingleCommand.LastIndexOf(";") + 1).Trim();    // kinda kludging to reuse code for now.

                // I'm going to cheat and set up some test strings,
                // just to save some typing.
                // Ob remove when live.
                switch (strCmd)
                {
                case "test usage;":
                    DatabaseContext database = new DatabaseContext(@"C:\temp\DatabaseName"); // or Mac path, etc.
                    CommandParser parser = new CommandParser(database);
                    string sql = @"CREATE TABLE TestTable (
                                            ID INTEGER (4) AUTOINCREMENT,
                                            CITY CHAR (10));";
                    parser.executeCommand(sql);
                    parser.executeCommand("INSERT INTO TestTable (CITY) VALUES ('New York');");
                    parser.executeCommand("INSERT INTO TestTable (CITY) VALUES ('Boston');");
                    parser.executeCommand("INSERT INTO TestTable (CITY) VALUES ('Fuquay');");
                    sql = "SELECT * FROM TestTable;";
                    DataTable dataTable = (DataTable)parser.executeCommand(sql);
                    strReturn += InfrastructureUtils.dataTableToString(dataTable);
                    parser.executeCommand("DROP TABLE TestTable;");

                    strCmd = "";
                    goto case "goto kludge";

                case "test create;":
                    strTestTableName = "jive" + DateTime.Now.Ticks;
                    strCmd = @"create TABLE " + strTestTableName + @"
                                        (ID INTEGER (4) AUTOINCREMENT,
                                        CITY CHAR (10),
                                        STATE CHAR (2),
                                        LAT_N REAL (5),
                                        LONG_W REAL (5),
                                        TIMESTAMP DATETIME(8));";
                    goto case "goto kludge";

                case "test create2;":
                    strTestTableName = "rest" + DateTime.Now.Ticks;
                    strCmd = @"create TABLE " + strTestTableName + @"
                                        (
                                        ID INTEGER (4) AUTOINCREMENT,
                                        CityId INTEGER (4),
                                        Name CHAR (30)
                                        );";
                    goto case "goto kludge";

                case "test select;":
                    strCmd = @"SELECT ID, CITY, LAT_N FROM "
                                        + strTestTableName + @"
                                        INNER JOIN rest
                                        ON jive.Id = rest.CityId
                                        WHERE city = 'Chucktown' AND LAT_N > 32;";
                    goto case "goto kludge";

                case "test insert;":
                    strCmd = @"INSERT INTO " + strTestTableName + @" (CITY, STATE, LAT_N, LONG_W, TIMESTAMP)
                                        VALUES ('Chucktown', 'SC', 32.776, 79.931, '12/12/2001');";
                    goto case "goto kludge";

                case "insert2;":
                    strCmd = @"INSERT INTO " + strTestTableName + @" (CITY, LAT_N, LONG_W, TIMESTAMP) VALUES ('Chucktown', 32.776, 79.931, '11/11/2011');";
                    goto case "goto kludge";

                case "test drop;":
                    strCmd = @"DROP TABLE " + strTestTableName + @";";
                    goto case "goto kludge";

                case "test update;":
                    strCmd = @"UPDATE jive SET city = 'Gotham', LAT_N = 45.987 WHERE ID = 4;";
                    goto case "goto kludge";

                    // Okay, Lippert doesn't say to use it quite like this, but he did give me the idea:
                    // http://blogs.msdn.com/b/ericlippert/archive/2009/08/13/four-switch-oddities.aspx
                case "goto kludge":
                    strReturn += "Test cmd:" + Environment.NewLine + strCmd + System.Environment.NewLine;
                    break;
                }

                // Execute the command.
                if (strCmd.ToLower().StartsWith("use"))
                {
                    string strDbPath = strCmd.Split()[1].TrimEnd(';');
                    // Complete kludge.  Use regexp and check for xplat formats.
                    if (strDbPath.StartsWith(@"C:\"))
                        strParentDir = strDbPath;
                    else
                        strParentDir = Utils.cstrHomeDir + System.IO.Path.DirectorySeparatorChar + strDbPath;
                    database = new DatabaseContext(strParentDir);
                    strReturn += strParentDir;
                }
                else if (strParentDir.Equals(""))
                {
                    strReturn += "Please select a database before executing a statement.";
                }
                else
                {
                    try
                    {
                        CommandParser parser = new CommandParser(database);
                        object objResult = parser.executeCommand(strCmd);

                        if (objResult is DataTable)
                        {
                            strReturn += InfrastructureUtils.dataTableToString((DataTable)objResult, intLineLength);
                        }
                        else if (objResult is string)
                        {
                            strReturn += objResult.ToString();
                        }
                        else if (strCmd.Trim().ToUpper().StartsWith("INSERT"))
                        {
                            strReturn += "Row ID for new row is: " + objResult;
                        }
                        else if (strCmd.Trim().ToUpper().StartsWith("SELECT MAX("))
                        {
                            strReturn += objResult.ToString();
                        }
                        else
                        {
                            strReturn += "Uncaptured return value.";
                        }
                    }
                    catch (Exception e)
                    {
                        strReturn += "Error executing statement.\n\t" + e.Message;
                    }

                }
            }   // end of "foreach" command in the split commands queue.

            return strReturn;
        }

    }
}

