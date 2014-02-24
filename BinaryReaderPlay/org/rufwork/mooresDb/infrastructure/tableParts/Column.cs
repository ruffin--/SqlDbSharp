// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.Linq;

namespace org.rufwork.mooresDb.infrastructure.tableParts
{
    // TODO: Consider putting enum into its own file.
    //
    // I should probably return and handle DECIMAL correctly.
    // See DECIMAL fun here: stackoverflow.com/questions/618535/
    //
    // Note that single byte-long types are handled differently when
    // written to a table's metadata row, and have their own entries 
    // here.
    public enum COLUMN_TYPES
    {
        // Note that removing or adding new types (other than at the end) will break backwards compat (of all those millions of apps using this)
        // since the number is written right into the header of the database file.
        AUTOINCREMENT,  // I'm kludging autoincrement a bit too much.  With this setup, I might add AUTOINCREMENT_8 later for a "near-long".
        SINGLE_CHAR,
        BIT,
        CHAR,
        INT,
        FLOAT,
        DECIMAL,
        DATETIME,
        TINYINT
    }

    // Convenience class to keep track of columns
    public class Column
    {
        public COLUMN_TYPES colType;
        public string strColName;
        public int intColStart;
        public int intColLength;

        // TODO: Why isn't this on the TableContext?
        public int intAutoIncrementCount;   // Another kludge, but this is the cleanest way not to affect the rest of the code.

        public int intColIndex;
        public bool isFuzzyName;    // there's a horrible kludge in the way names are stored that make it where we might not a full column name here.
                                    // if strColName is potentially not a full name, isFuzzyName is set to true.
                                    // Explained in more detail in TableContext._prepareTableFromFile().
        public int intColEnd
        {
            get
            {
                return this.intColStart + this.intColLength;
            }
        }

        public static bool IsSingleByteType(COLUMN_TYPES colType)
        {
            COLUMN_TYPES[] asingleByteTypes = new COLUMN_TYPES[] { 
                COLUMN_TYPES.BIT,
                COLUMN_TYPES.SINGLE_CHAR,
                COLUMN_TYPES.TINYINT
            };
            return asingleByteTypes.Contains(colType);
        }
    }
}

