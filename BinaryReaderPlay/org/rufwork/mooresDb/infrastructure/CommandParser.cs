// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;

using System.Collections.Generic;
using org.rufwork.mooresDb.infrastructure.commands;
using System.Text.RegularExpressions;
using org.rufwork.mooresDb.infrastructure.contexts;
using org.rufwork.mooresDb.exceptions;

namespace org.rufwork.mooresDb.infrastructure
{
    public class CommandParser
    {
        //string strDatabaseLoc = null;
        private DatabaseContext _database;

        // TODO: So, obviously, make a base ICommand or CommandBase that everything can implement/extend.
        // ????: What's the advantage of scoping these at object level, anyhow?  <<< What he said.  For now, lemming it up, boah.
        private CreateTableCommand _createTableCommand;
        private InsertCommand _insertCommand;
        private SelectCommand _selectCommand;
        private DeleteCommand _deleteCommand;
        private UpdateCommand _updateCommand;   // Two lemmings don't make a wren.

        public CommandParser (DatabaseContext database)
        {
            _database = database;
        }

        public object executeCommand(string strSql)    {
            object objReturn = null;

            strSql = Utils.BacktickQuotes(strSql); // TODO: WHOA!  Super kludge for single quote escapes.  See "Grave accent" in idiosyncracies.

            // TODO: This is assuming a single command.  Add splits by semi-colon.
            if (!strSql.Trim().EndsWith(";"))
            {
                throw new Exception("Unterminated command.");
            }

            strSql = strSql.TrimEnd(';');

            string[] astrCmdTokens = Utils.StringToNonWhitespaceTokens2(strSql);

            // TODO: Want to ISqlCommand this stuff -- we need to have execute
            // methods that don't take strings but "command tokens".
            switch (astrCmdTokens[0].ToLower())    {
                case "insert":
                    _insertCommand = new InsertCommand(_database); // TODO: This is too much repeat instantiation.  Rethink that.
                    objReturn = _insertCommand.executeInsert(strSql);
                    break;
                
                case "select":
                    _selectCommand = new SelectCommand(_database);
                    objReturn = _selectCommand.executeStatement(strSql);
                    break;
                
                case "delete":
                    _deleteCommand = new DeleteCommand(_database);
                    _deleteCommand.executeStatement(strSql);
                    objReturn = "DELETE executed."; // TODO: Add ret val of how many rows returned
                    break;

                case "update":
                    _updateCommand = new UpdateCommand(_database);
                    _updateCommand.executeStatement(strSql);
                    objReturn = "UPDATE executed."; // TODO: Add ret val of how many rows returned
                    break;

                case "create":
                    _createTableCommand = new CreateTableCommand(_database);
                    _createTableCommand.executeStatement(strSql);
                    objReturn = "Table created.";
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

