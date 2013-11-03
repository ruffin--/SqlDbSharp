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

namespace org.rufwork.mooresDb.infrastructure
{
    public class CommandParser
    {
        //string strDatabaseLoc = null;
        private DatabaseContext _database;

        // TODO: So, obviously, make a base ICommand or CommandBase that everything can implement/extend.
        // ????: What's the advantage of scoping these at object level, anyhow?
        private CreateTableCommand _createTableCommand;
        private InsertCommand _insertCommand;
        private SelectCommand _selectCommand;
        private DeleteCommand _deleteCommand;

        public CommandParser (DatabaseContext database)
        {
            _database = database;
        }

        public object executeCommand(string strSql)    {
            object objReturn = null;

            strSql = strSql.Replace("''", "`"); // TODO: WHOA!  Super kludge for single quote escapes.  See "Grave accent" in idiosyncracies.

            // TODO: This is assuming a single command.  Add splits by semi-colon.
            string patternEndsSemiColon = @";\w*$";
            Match endsWithSemiColonMatch = Regex.Match(strSql, patternEndsSemiColon, RegexOptions.IgnoreCase);

            if (!endsWithSemiColonMatch.Success)
            {
                throw new Exception("Unterminated command.");
            }

            strSql = strSql.TrimEnd(';');

            string[] astrCmdTokens = Utils.stringToNonWhitespaceTokens2(strSql);
                
            // TODO: Want to ISqlCommand this stuff -- we need to have execute
            // methods that don't take strings but "command tokens".
            switch (astrCmdTokens[0].ToLower())    {
                case "insert":
                    _insertCommand = new InsertCommand(_database); // TODO: This is too much repeat instantiation.  Rethink that.
                    _insertCommand.executeInsert(strSql);
                    break;
                
                case "select":
                    _selectCommand = new SelectCommand(_database);
                    objReturn = _selectCommand.executeStatement(strSql);
                    break;
                
                case "delete":
                    _deleteCommand = new DeleteCommand(_database);
                    _deleteCommand.executeStatement(strSql);
                    break;

                case "update":
                    throw new NotImplementedException();

                case "create":
                    _createTableCommand = new CreateTableCommand(_database);
                    _createTableCommand.executeStatement(strSql);
                    break;

                case "drop":
                    DropTableCommand dropTableCommand = new DropTableCommand(_database);
                    dropTableCommand.executeStatement(strSql);
                    break;

                default:
                    throw new Exception("Syntax error: Unhandled command type.");
            }

            return objReturn;
        }
    }
}

