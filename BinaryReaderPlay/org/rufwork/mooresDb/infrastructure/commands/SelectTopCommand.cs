// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

using org.rufwork.mooresDb.infrastructure.contexts;
using org.rufwork.extensions;
using org.rufwork.mooresDb.exceptions;

namespace org.rufwork.mooresDb.infrastructure.commands
{
    public class SelectTopCommand
    {
        private DatabaseContext _database;

        public SelectTopCommand(DatabaseContext database)
        {
            _database = database;
        }

        public object executeStatement(string strSql)
        {
            int intN = -1;

            // For now, I'm just keeping this as simple as possible.
            // Later, we'll check for indexed fields and do this much
            // more intelligently and efficiently.
            string[] astrTokes = strSql.StringToNonWhitespaceTokens2();

            if (
                !astrTokes[0].Equals("SELECT", StringComparison.CurrentCultureIgnoreCase)
                ||
                !astrTokes[1].Equals("TOP", StringComparison.CurrentCultureIgnoreCase)
                ||
                !int.TryParse(astrTokes[2], out intN)
            )
            {
                throw new SyntaxException("Illegal format for SELECT TOP");
            }

            string strPlainSql = "SELECT " + string.Join(" ", astrTokes.Skip(3));

            SelectCommand selectCommand = new SelectCommand(_database);
            DataTable table = (DataTable)selectCommand.executeStatement(strPlainSql);
            table = table.SelectTopNRows(intN);
            
            return table;
        }
    }
}
