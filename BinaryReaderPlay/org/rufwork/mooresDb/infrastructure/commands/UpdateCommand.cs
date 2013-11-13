// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Data;
using System.IO;
using System.Text.RegularExpressions;
using org.rufwork.mooresDb.infrastructure.tableParts;
using org.rufwork.mooresDb.infrastructure.serializers;
using org.rufwork.mooresDb.infrastructure.contexts;
using org.rufwork.mooresDb.infrastructure;
using org.rufwork.mooresDb.infrastructure.commands.Processors;
using org.rufwork.utils;

namespace org.rufwork.mooresDb.infrastructure.commands
{
    public class UpdateCommand
    {
        private TableContext _table;
        private DatabaseContext _database;

        public UpdateCommand(DatabaseContext database)
        {
            _database = database;
        }

        // TODO: Create Command interface
        public bool executeStatement(string strSql)
        {
            bool wasSuccessful = false;

            // TODO: Think how to track multiple tables/TableContexts
            // Note that the constructor will set up the table named
            // in the SELECT statement in _table.
            CommandParts updateParts = new CommandParts(_database, _table, strSql, CommandParts.COMMAND_TYPES.UPDATE);

            if (MainClass.bDebug)
            {
                Console.WriteLine("SELECT: " + updateParts.strSelect);
                Console.WriteLine("FROM: " + updateParts.strFrom);
                if (!string.IsNullOrEmpty(updateParts.strInnerJoinKludge))
                {
                    throw new Exception("Syntax error: INNER JOIN in an UPDATE statement is not supported: " + strSql);
                }
                Console.WriteLine("WHERE: " + updateParts.strWhere);    // Note that WHEREs aren't applied to inner joined tables right now.
                Console.WriteLine("ORDER BY: " + updateParts.strOrderBy);
            }

            _table = _database.getTableByName(updateParts.strTableName);

            DataTable dtThrowAway = new DataTable();
            WhereProcessor.ProcessRows(ref dtThrowAway, _table, updateParts);

            return wasSuccessful;
        }

        public static void Main(string[] args)
        {
            string strSql = @"UPDATE jive SET city = 'Gotham', LAT_N = 45.987 WHERE ID = 8";

            DatabaseContext database = new DatabaseContext(MainClass.cstrDbDir);

            UpdateCommand updateCommand = new UpdateCommand(database);
            updateCommand.executeStatement(strSql);

            Console.WriteLine("Return to quit.");
            Console.Read();

        }
    }
}