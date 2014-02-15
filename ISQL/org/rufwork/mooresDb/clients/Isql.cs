﻿// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using org.rufwork.mooresDb.infrastructure;
using org.rufwork.mooresDb.infrastructure.contexts;
using System.Data;
using System.IO;

namespace org.rufwork.mooresDb.clients
{
    class Isql
    {
        static void Main(string[] args)
        {
            string strInput = "";
            string strParentDir = "";
            string strTestTableName = "jive";

            try
            {
                Console.SetWindowSize(160, 50);
            }
            catch (Exception)
            {
                Console.WriteLine("Screen doesn't support chosen console window dimensions.");
            }

            DatabaseContext dbTemp = null;

            Console.SetIn(new StreamReader(Console.OpenStandardInput(4096)));
            Console.WriteLine("Embed DB isql client.\nType a single statement followed by a semi-colon and return to execute, or a period by itself to quit.\n\n");

            // set up debug db
            Console.WriteLine("Starting at testing dir: MooresDbPlay");
            strParentDir = Utils.cstrHomeDir + System.IO.Path.DirectorySeparatorChar + "MooresDbPlay";
            dbTemp = new DatabaseContext(strParentDir);
            Console.WriteLine(strParentDir + "\n\n");
            // eo setup debug db

            while (!strInput.Equals("."))
            {
                string strCmd = "";
                strInput = "";

                try
                {
                    // Put the command together.
                    while (!strInput.Equals(".") && -1 == strInput.IndexOf(";")) 
                    {
                        strInput = Console.ReadLine();
                        if (!strInput.StartsWith("--"))
                        {
                            strCmd += strInput + " ";
                        }
                    }

                    if (!strCmd.Trim().Equals("."))
                    {
                        strCmd = strCmd.Substring(0, strCmd.IndexOf(";")+1).Trim();    // TODO: Could potentially split into multiple commands with more than one ;
                        strCmd = Utils.removeNewlines(strCmd, " ");
                        
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
                                Console.WriteLine(InfrastructureUtils.dataTableToString(dataTable));
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
                                Console.WriteLine("Test cmd:" + Environment.NewLine + strCmd);
                                break;
                        }

                        // Execute the command.
                        if (strCmd.ToLower().StartsWith("use"))
                        {
                            strParentDir = Utils.cstrHomeDir + System.IO.Path.DirectorySeparatorChar + strCmd.Split()[1].TrimEnd(';');
                            dbTemp = new DatabaseContext(strParentDir);
                            Console.WriteLine(strParentDir);
                        }
                        else if (strParentDir.Equals(""))
                        {
                            Console.WriteLine("Please select a database before executing a statement.");
                        }
                        else
                        {
                            try
                            {
                                CommandParser parser = new CommandParser(dbTemp);
                                object objResult = parser.executeCommand(strCmd);

                                if (objResult is DataTable)
                                {
                                    Console.WriteLine(InfrastructureUtils.dataTableToString((DataTable)objResult));
                                }
                                else if (objResult is string)
                                {
                                    Console.WriteLine(objResult.ToString());
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Error executing statement.\n\t" + e.Message);
                            }
                        
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Something borked: " + ex.ToString());
                    strParentDir = "";
                }
                Console.WriteLine(System.Environment.NewLine + System.Environment.NewLine);
            }
        }
    }
}
