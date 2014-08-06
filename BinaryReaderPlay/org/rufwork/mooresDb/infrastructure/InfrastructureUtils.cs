// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using org.rufwork.mooresDb.infrastructure.tableParts;
using org.rufwork.shims.data;
using org.rufwork.mooresDb.infrastructure.contexts;

namespace org.rufwork.mooresDb.infrastructure
{
    public class InfrastructureUtils
    {

        public static StringComparison caseSetting = StringComparison.CurrentCultureIgnoreCase;

        public static string dataTableToString(DataTable dtIn)
        {
            string strReturn = "";
            int[] aintColLength = new int[dtIn.Columns.Count];

            for (int i = 0; i < dtIn.Columns.Count; i++)
            {
                DataColumn dc = dtIn.Columns[i];
                aintColLength[i] = dc.ColumnName.Length;    // start with a minimum display length of the column name.

                // Figure out how many characters might be in this column
                // for the current data.  I'm going to ignore performance for now.

                foreach (DataRow dr in dtIn.Rows)
                {
                    if (dr[dc].ToString().Length > aintColLength[i])
                    {
                        aintColLength[i] = dr[dc].ToString().Length;
                    }
                }

                if (dc.ColumnName.Length > aintColLength[i])
                {
                    strReturn += dc.ColumnName.Substring(0, aintColLength[i]) + " | ";
                }
                else
                {
                    strReturn += dc.ColumnName.PadRight(aintColLength[i]) + " | ";
                }
            }
            strReturn += System.Environment.NewLine;

            foreach (DataRow dr in dtIn.Rows)
            {
                // TODO: Is foreach Column order guaranteed?
                for (int i = 0; i < dtIn.Columns.Count; i++)
                {
                    strReturn += dr[dtIn.Columns[i]].ToString().PadLeft(aintColLength[i]) + " # ";
                }
                strReturn += System.Environment.NewLine;
            }

            return strReturn;
        }

        // I could do this with DataSets and DataRelations, but this 
        // is more straightfoward for now, I think.
        static public DataTable equijoinTables(DataTable dt1, DataTable dt2, 
            string strDt1JoinColName_Raw, string strDt2JoinColName_Raw
        )
        {
            DataTable dtReturn = new DataTable();
            Dictionary<string, string> dictTable2ColAliases = new Dictionary<string,string>();

            if (null == strDt1JoinColName_Raw || null == strDt2JoinColName_Raw)
            {
                throw new Exception("Column name not found in join: " + dt1.TableName + " :: " + dt2.TableName);
            }

            // Make sure the two join columns are of the same type before starting.
            if (!dt1.Columns[strDt1JoinColName_Raw].DataType.Equals(dt2.Columns[strDt2JoinColName_Raw].DataType))
            {
                throw new Exception("DataTable join column data types do not match: "
                    + dt1.TableName + "." + strDt1JoinColName_Raw
                    + " :: "
                    + dt2.TableName + "." + strDt2JoinColName_Raw);
            }

            foreach (DataColumn dc in dt1.Columns)
            {
                DataColumn colForDt = new DataColumn(dc.ColumnName);
                colForDt.DataType = dc.DataType;
                dtReturn.Columns.Add(colForDt);
            }

            foreach (DataColumn dc in dt2.Columns)
            {
                string strColName = dc.ColumnName;

                // Check for names duplicated in both tables.
                // Alias dt2's if there's a conflict.
                if (dtReturn.Columns.Contains(strColName))
                {
                    // TODO: Of course this misses "Table2.Name' (inclusive of "Table2" -- see?  Crazy outlier.) as a column name for Table 1.
                    // We'll risk that for now.  It really shouldn't happen.
                    strColName = dt2.TableName + "." + strColName;
                    dictTable2ColAliases.Add(dc.ColumnName, strColName);
                }
                DataColumn colForDt = new DataColumn(strColName);
                colForDt.DataType = dc.DataType;
                dtReturn.Columns.Add(colForDt);
            }

            // Moore's Comparison it is.  TODO: Make it isn't. [sic]
            foreach (DataRow dr in dt1.Rows)
            {
                foreach (DataRow dr2 in dt2.Rows)
                {
                    if (dr[strDt1JoinColName_Raw].Equals(dr2[strDt2JoinColName_Raw]))
                    {
                        DataRow rowNew = dtReturn.NewRow();
                        // Copy the values from both rows into dtReturn.
                        foreach (DataColumn dc1 in dt1.Columns)
                        {
                            rowNew[dc1.ColumnName] = dr[dc1];
                        }

                        foreach (DataColumn dc2 in dt2.Columns)
                        {
                            string strColName = dc2.ColumnName;
                            if (dictTable2ColAliases.ContainsKey(strColName))
                            {
                                strColName = dictTable2ColAliases[strColName];
                            }

                            rowNew[strColName] = dr2[dc2];
                        }
                        dtReturn.Rows.Add(rowNew);
                    }
                }
            }

            return dtReturn;
        }

        // <summary>
        // Returns which COLUMN_TYPES enum value matches this type name.
        // Eg, translates CHAR or VARCHAR into COLUMN_TYPES.SINGLE_CHAR 
        // or COLUMN_TYPES.CHAR.
        // </summary>
        static public COLUMN_TYPES? colTypeFromString(string strTypeName, int intByteLength, string strModifier)
        {
            COLUMN_TYPES? colType = null;

            if (StringComparison.CurrentCultureIgnoreCase == InfrastructureUtils.caseSetting) strTypeName = strTypeName.ToUpper();

            switch (strTypeName)
            {
                case "CHAR":
                case "VARCHAR":
                    if (1 == intByteLength)
                    {
                        colType = COLUMN_TYPES.SINGLE_CHAR;
                    }
                    else
                    {
                        colType = COLUMN_TYPES.CHAR;
                    }
                    break;

                // TODO: True BIT column type.
                // Check BIT here: http://dev.mysql.com/doc/refman/5.1/en/bit-type.html
                case "TINYINT":
                case "BIT":
                    // If == 1, great.  If < 1, then probably nothing explicitly said.
                    // Will take default length of 1, below, so let it continue.
                    if (1 < intByteLength)
                    {
                        throw new Exception("TINYINT columns must have a length of 1.");
                    }
                    colType = COLUMN_TYPES.TINYINT;
                    break;

                case "INT":
                case "INTEGER":
                    if (null != strModifier 
                        && (
                            strModifier.IndexOf("AUTO_INCREMENT", InfrastructureUtils.caseSetting) > -1
                            || strModifier.IndexOf("AUTOINCREMENT", InfrastructureUtils.caseSetting) > -1
                           )
                        )
                    {
                        colType = COLUMN_TYPES.AUTOINCREMENT;
                    }
                    else if (1 == intByteLength)
                    {
                        colType = COLUMN_TYPES.TINYINT;
                    }
                    else
                    {
                        colType = COLUMN_TYPES.INT;
                    }
                    break;

                case "FLOAT":
                case "DECIMAL": // so that's not really accurate.
                case "REAL":
                    colType = COLUMN_TYPES.FLOAT;
                    break;

                case "DATE":
                case "DATETIME":
                    colType = COLUMN_TYPES.DATETIME;
                    break;

                default:
                    throw new Exception("Illegal column type: " + strTypeName);
            }

            return colType;
        }
    }
}
