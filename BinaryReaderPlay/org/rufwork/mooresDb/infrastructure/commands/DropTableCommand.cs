// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;

using System.Collections.Generic;
using System.Text;

using org.rufwork.mooresDb.infrastructure.tableParts;
using org.rufwork.mooresDb.infrastructure.serializers;
using org.rufwork.mooresDb.infrastructure.contexts;
using System.IO;

using org.rufwork.extensions;

namespace org.rufwork.mooresDb.infrastructure.commands
{
    public class DropTableCommand
    {
        private TableContext _table;
        private DatabaseContext _database;

        public DropTableCommand(DatabaseContext database)
        {
            _database = database;
        }

        public void executeStatement(string strSql)
        {
            bool bIgnoreMissingTable = false;

            // NOTE: Forced case insensitivity.
            strSql = strSql.ToLower().TrimEnd(';');

            string[] astrCmdTokens = strSql.StringToNonWhitespaceTokens2();

            bool bQuickTokenCheck = astrCmdTokens.Length >= 3
                && "drop" == astrCmdTokens[0].ToLower() 
                && "table" == astrCmdTokens[1].ToLower();

            if (!bQuickTokenCheck)
            {
                throw new Exception("Illegal drop command -- Syntax DROP TABLE TableName;");
            }
            else
            {
                string strTableName = astrCmdTokens[2];

                if (astrCmdTokens.Length >= 5 
                        && astrCmdTokens[2].Equals("if", StringComparison.CurrentCultureIgnoreCase)
                        && astrCmdTokens[3].Equals("exists", StringComparison.CurrentCultureIgnoreCase))
                {
                    bIgnoreMissingTable = true;
                    strTableName = astrCmdTokens[4];
                }
                strTableName = strTableName.Trim('`');
                
                _table = _database.getTableByName(strTableName);
                if (null == _table)
                {
                    if (!bIgnoreMissingTable)
                    {
                        throw new Exception("Table not found in database: " + strTableName);
                    }
                }
                else
                {
                    File.Delete(_table.strTableFileLoc);
                    _database.removeExistingTable(_table);
                }
            }
        }
    }
}