// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;

using System.Collections.Generic;
using System.Text.RegularExpressions;
using org.rufwork.shims.data; // using System.Data;

using org.rufwork.mooresDb.infrastructure.commands;
using org.rufwork.mooresDb.infrastructure.contexts;
using org.rufwork.mooresDb.exceptions;
using org.rufwork.extensions;

namespace org.rufwork.mooresDb.infrastructure
{
    public class CommandParser
    {
        //string strDatabaseLoc = null;
        private DatabaseContext _database;
        private bool _logSql = false;
        private string _logLoc = string.Empty;

        // TODO: So, obviously, make a base ICommand or CommandBase that everything can implement/extend.
        // ????: What's the advantage of scoping these at object level, anyhow?  <<< What he said.  For now, lemming it up, boah.
        private CreateTableCommand _createTableCommand;
        private InsertCommand _insertCommand;
        private SelectCommand _selectCommand;
        private DeleteCommand _deleteCommand;
        private UpdateCommand _updateCommand;   // Two lemmings don't make a wren.

        public CommandParser (DatabaseContext database, bool logStatements = false)
        {
            _database = database;
            _logSql = logStatements;
        }

        // TODO: Not any serious savings here; currently solely a convenience class.
        public object executeScalar(string strSql)
        {
            object objReturn = null;

            if (_logSql)
                System.IO.File.AppendAllText (_database.strLogLoc, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ": "
                    + strSql + System.Environment.NewLine);


            // Not sure if I want CommandParser to know about DataTable.  I think that's okay.
            DataTable dtTemp = (DataTable)this.executeCommand(strSql);
            if (dtTemp.Rows.Count > 0)
            {
                objReturn = dtTemp.Rows[0][0];
            }

            return objReturn;
        }

        public object executeCommand(string strSql)
        {
            object objReturn = null;

            if (_logSql)
                System.IO.File.AppendAllText (_database.strLogLoc, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ": "
                    + strSql + System.Environment.NewLine);

            strSql = strSql.RemoveNewlines(" ").BacktickQuotes(); // TODO: WHOA!  Super kludge for single quote escapes.  See "Grave accent" in idiosyncracies.

            // Sorry, I got tired of forgetting this.
            //if (!strSql.Trim().EndsWith(";"))
            //{
            //    throw new Exception("Unterminated command.");
            //}

            // TODO: This is assuming a single command.  Add splits by semi-colon.
            strSql = strSql.TrimEnd(';');

            string[] astrCmdTokens = strSql.StringToNonWhitespaceTokens2();    // TODO: We're almost always immediately doing this again in the executeStatements.

            // TODO: Want to ISqlCommand this stuff -- we need to have execute
            // methods that don't take strings but "command tokens".
            // TODO: Once indicies are live, we're going to have to check for them
            // after any CUD operations.
            switch (astrCmdTokens[0].ToLower())    {
                case "insert":
                    _insertCommand = new InsertCommand(_database); // TODO: This is too much repeat instantiation.  Rethink that.
                    objReturn = _insertCommand.executeInsert(strSql);
                    System.Diagnostics.Debug.Print("Update any indicies");
                    break;
                
                case "select":
                    switch (astrCmdTokens[1].ToLower())
                    {
                        case "max":
                            SelectMaxCommand selectMaxCmd = new SelectMaxCommand(_database);
                            objReturn = selectMaxCmd.executeStatement(strSql);
                            break;

                        default:
                            _selectCommand = new SelectCommand(_database);
                            objReturn = _selectCommand.executeStatement(strSql);
                            break;
                    }
                    break;
                
                case "delete":
                    _deleteCommand = new DeleteCommand(_database);
                    _deleteCommand.executeStatement(strSql);
                    objReturn = "DELETE executed."; // TODO: Add ret val of how many rows returned
                    System.Diagnostics.Debug.Print("Update any indicies");
                    break;

                case "update":
                    _updateCommand = new UpdateCommand(_database);
                    _updateCommand.executeStatement(strSql);
                    objReturn = "UPDATE executed."; // TODO: Add ret val of how many rows returned
                    System.Diagnostics.Debug.Print("Update any indicies");
                    break;

                case "create":
                    switch (astrCmdTokens[1].ToLower())
                    {
                        case "table":
                            _createTableCommand = new CreateTableCommand(_database);
                            objReturn = _createTableCommand.executeStatement(strSql);
                            break;

                        case "index":
                            CreateIndexCommand createIndexCommand = new CreateIndexCommand(_database);
                            objReturn = createIndexCommand.executeStatement(strSql);
                            break;
                    }
                    
                    break;

                case "drop":
                    DropTableCommand dropTableCommand = new DropTableCommand(_database);
                    dropTableCommand.executeStatement(strSql);
                    objReturn = @"Table dropped (or, if ""IF EXISTS"" was used, dropped iff found).";   // TODO: These are pretty sorry messages.  Have the executeStatement return something more informative.
                    break;

                default:
                    throw new SyntaxException("Syntax error: Unhandled command type.");
            }

            return objReturn;
        }
    }
}

