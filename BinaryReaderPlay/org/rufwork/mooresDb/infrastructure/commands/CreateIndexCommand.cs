// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using org.rufwork.mooresDb.infrastructure.contexts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using org.rufwork.extensions;
using System.IO;
using org.rufwork.mooresDb.infrastructure.tableParts;
using org.rufwork.mooresDb.infrastructure.serializers;

namespace org.rufwork.mooresDb.infrastructure.commands
{
    #region ByteComparer
    // ByteComparer code is...
    // Copyright (c) 2008-2013 Hafthor Stefansson
    // Distributed under the MIT/X11 software license
    // Ref: http://www.opensource.org/licenses/mit-license.php.
    class ByteComparer : IComparer<byte[]>
    {
        public int Compare(byte[] x, byte[] y)
        {
            var len = Math.Min(x.Length, y.Length);
            for (var i = 0; i < len; i++)
            {
                var c = x[i].CompareTo(y[i]);
                if (c != 0)
                {
                    return c;
                }
            }

            return x.Length.CompareTo(y.Length);
        }
    }
    #endregion ByteComparer

    public class CreateIndexCommand
    {
        private DatabaseContext _database;

        public CreateIndexCommand (DatabaseContext database)
        {
            _database = database;
        }

        public object executeStatement(string strSql)
        {
            string strErr = string.Empty;

            try
            {
                // We'll have already checked for "CREATE INDEX". Go ahead and remove that.
                strSql = strSql.Substring("CREATE INDEX ".Length);

                string[] astrTokes = strSql.StringToNonWhitespaceTokens2();

                foreach (string str in astrTokes)
                {
                    Console.WriteLine(str);
                }

                string strIndexName = astrTokes[0];
                string strTableName = astrTokes[2];
                string strColName = astrTokes[3];

                TableContext table = _database.getTableByName(strTableName);
                Column column = table.getColumnByName(strColName);

                if (null == column)
                {
                    throw new Exception("Column " + strColName + " not found in table " + strTableName);
                }

                Dictionary<int, byte[]> dictRows = new Dictionary<int, byte[]>();

                string strIndexLoc = table.strTableFileLoc.Substring(0, table.strTableFileLoc.Length - 5) + ".mdbi";

                Column colFauxIndex = new Column("rowNum", COLUMN_TYPES.INT, column.intColLength, 4);
                IntSerializer intSerializer = new IntSerializer(colFauxIndex);

                using (BinaryReader b = new BinaryReader(File.Open(table.strTableFileLoc, FileMode.Open)))
                {
                    int intRowCount = table.intFileLength / table.intRowLength;
                    b.BaseStream.Seek(2 * table.intRowLength, SeekOrigin.Begin);  // TODO: Code more defensively in case it's somehow not the right/minimum length

                    for (int i = 2; i < intRowCount; i++)
                    {
                        byte[] abytRow = b.ReadBytes(table.intRowLength);

                        // Check and make sure this is an active row, and has 
                        // the standard row lead byte, 0x11.  If not, the row
                        // should not be read.
                        switch (abytRow[0])
                        {
                            case 0x88:
                                // DELETED
                                break;

                            case 0x11:
                                // ACTIVE
                                byte[] abytCol = new byte[column.intColLength];
                                Array.Copy(abytRow, column.intColStart, abytCol, 0, column.intColLength);
                                dictRows.Add(i, abytCol);
                                break;

                            default:
                                throw new Exception("Unexpected row state in CREATE INDEX: " + abytRow[0]);
                        }
                    }
                }

                // now write the index to a file.
                using (FileStream stream = new FileStream(strIndexLoc, FileMode.Create))
                {
                    using (BinaryWriter writer = new BinaryWriter(stream))
                    {
                        foreach (KeyValuePair<int, byte[]> kvp in dictRows.OrderBy(kvp => kvp.Value, new ByteComparer()))
                        {
                            writer.Write(kvp.Value);
                            writer.Write(intSerializer.toByteArray(kvp.Key.ToString()));
                        }
                        writer.Flush();
                    }
                }
            }
            catch (Exception e)
            {
                strErr = e.ToString();
            }

            return string.IsNullOrWhiteSpace(strErr) ? "Index created" : strErr;
        }
    }
}
