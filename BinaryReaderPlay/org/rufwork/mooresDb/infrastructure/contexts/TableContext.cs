// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

using org.rufwork.mooresDb.infrastructure.tableParts;
using org.rufwork.mooresDb.exceptions;

namespace org.rufwork.mooresDb.infrastructure.contexts
{
    public class TableContext // I think this is going to be "aka Table"
    {
        #region props 'n' fields

        public bool bIgnoreColNameCase = true;  // TODO: Handle more deliberately.
        // Case sensitivity really probably should be a database-level concept, though I've
        // brilliantly separated the table objects from any concept of their database parent.
        // Brilliant.  Guiness time.

        // Editing to make this read-only and live.
        // TODO: Probably a decent amount of wasted overhead
        // creating a new FileInfo each time this is pulled.
        public int intFileLength
        {
            get
            {
                int intReturn = -1;

                try
                {
                    FileInfo f = new FileInfo(this.strTableFileLoc);
                    intReturn = (int)f.Length;
                }
                catch (Exception)
                {
                    intReturn = -1;
                }
                return intReturn;
            }
        }

        private string _strTableName = "";
        public string strTableName
        {
            get { return this._strTableName; }
        }
        private Column[] _columns = new Column[0];
        private string _strTableParentFolder = "";
        public string strTableParentFolder
        {
            get { return _strTableParentFolder; }
        }

        public string strTableFileLoc
        {
            get { return Path.Combine(_strTableParentFolder, strTableName + ".mdbf"); }
        }

        private int _intRowLength = -1;
        public int intRowLength
        {
            get
            {
                if (-1 == _intRowLength)
                {
                    throw new Exception("Cannot get the length of an unintialized table's row.");
                }
                return _intRowLength;
            }
        }
        #endregion props 'n' fields


        #region constructors
        public TableContext()
        {
            // for tables being created
        }

        public TableContext(string strTableLoc)
        {
            Console.WriteLine("table manager table loc constructor");
            _strTableParentFolder = strTableLoc;
        }

        public TableContext(string strTableLoc, string strTableName)
        {
            //Console.WriteLine("table manager table loc and table name constructor");

            _strTableParentFolder = strTableLoc;
            _strTableName = strTableName;
            _prepareTableFromFile();
        }
        #endregion constructors

        public void loadNewTable(string strTableName)
        {
            _strTableName = strTableName;
            _prepareTableFromFile();
        }

        // TODO: Is the only reason this is public is for the Main method test?
        public void prePrepareNewTable(string strTableLoc, string strTableName)
        {
            _strTableParentFolder = strTableLoc;
            _strTableName = strTableName;
            // _prepareTableFromFile();  // no, doofus, this is a new table you haven't made yet.
        }

        private void _prepareTableFromFile()
        {
            if (_strTableName.Equals("") || _strTableParentFolder.Equals(""))
            {
                throw new Exception("Attempted to prepare a table from a manager that has not been initialized.");
            }
            else
            {
                FileInfo info = new FileInfo(this.strTableFileLoc);
                // this is really the "come on, stop looking already" breakpoint.
                // TODO: Consider something less than 9,223,372,036,854,775,807 / 2.
                // Though, admittedly, that's a random thing to try and describe as
                // an absolute ceiling.  (Divided by 2 b/c there are always at least 
                // two lines of metadata at the start of a mdbf file.)
                long lngStopTryingAt = (info.Length / 2) + 1; // TODO: Laziness alert with the +1.
                if (MainClass.bDebug) Console.WriteLine("Preparing table from: " + info.FullName);

                int intPullSize = 500;
                int intLineLength = -1;     // we can't know until we've actually found the first 0x11 0x11 combo.
                int intStartingByte = 0;    // I'm currently just starting at 0, reading ~500 bytes from a stream, looking for 0x11, 0x11, then,
                // if I haven't found it yet, scrolling forward to read approximately the next 500, ad infinitum
                // This is, admittedly, *NOT* the best way to accomplish reading far enough into the stream to find 
                // the end of the metadata lines.
                bool bFoundLineEnd = false;
                while (!bFoundLineEnd)
                {
                    FileStream fs = File.Open(this.strTableFileLoc, FileMode.Open);
                    byte[] abytFirstLine = Utils.ReadToLength(fs, intPullSize + intStartingByte);
                    fs.Close(); // TODO: Keep open until you're done; closing now to avoid leaving it open when I break/stop when debugging

                    // Now see if we've gotten to the first 0x11, 0x11 line end.
                    // Note: The real disadvantage of this routine is if a field length
                    // is 4369 (decimal), which is 0x1111.  Ooops!  So 4369 is out as a length.
                    // (as is 69904-69919, etc etc, but let's pretend nobody's doing something
                    // that long for now. I mean, heck, 4369 is ludicrous enough).
                    for (int i = intStartingByte; i < abytFirstLine.Length - 1; i++)
                    {
                        if (0x11 == abytFirstLine[i] && 0x11 == abytFirstLine[i + 1])
                        {
                            bFoundLineEnd = true;
                            intLineLength = i + 2;    // add both 0x11s (remembering array is 0-based)
                            break;
                        }
                    }

                    // see if we found the end of the first line.
                    if (!bFoundLineEnd)
                    {
                        if (abytFirstLine.Length >= lngStopTryingAt)
                        {
                            throw new ImproperDatabaseFileException("Improperly formatted file, or file is not a database file: " + info.FullName);
                        }
                        intStartingByte += (intPullSize - 2);       // so we're just re-evaling /some/ of the bytes; no big loss.
                        if (0 == abytFirstLine.Length)
                        {           // we're out of bytes.
                            break;
                        }
                    }
                    else
                    {
                        // Start reading out column types and lengths

                        //=====================================
                        // Read out the column names.
                        //=====================================
                        // NOTES:
                        // 1.) Remember the Naming Kludge.  The length of the field has been linked to the length
                        // of the column name just to keep spacing in the table simpler (and stopping us from having
                        // table metadata tables.  So there are essentially two types of names.
                        //      a.) Names that are shorter than the length of the fields.
                        //              eg, "spam CHAR(20)"
                        //              20 > "spam".length, so we have the entire name in the name row.
                        //              0x73 0x70 0x61 0x6d 0x00 0x00 0x00... 0x11
                        //              s    p    a    m    zero zero zero... end
                        //
                        //      b.) Names that are as long or longer than their column length.
                        //              eg, "firstName CHAR(6)"
                        //              6 < "firstName".length
                        //              0x66 0x69 0x72 0x73 0x74 0x4e 0x11
                        //              f    i    r    s    t    N    end
                        //  Remember that each column ends with a 0x11 marker.  Excepting the first two metadata and
                        //  name rows, rows can contain 0x11 as well.
                        //  
                        //  I'm horribly tempted to not use ASCII and to derive a more efficient encoding scheme to store names, 
                        //  since we're only using [A-Za-z0-9] and $, #, _ -- 65 chars
                        //  http://stackoverflow.com/questions/954884/what-special-characters-are-allowed-in-t-sql-column-name
                        //  Unfortunately eight bits isn't enough to capture two of those.  65 still needs...
                        //  0 1 0 0 0 0 0 1 
                        //  It'd have to fit in one nibble (four bits (16 options)) to do anything cool *and simple*.
                        //
                        // 2.) There are a couple of ways of reading these lines that would be more complicated but also more efficient.  
                        // One would be to see if our abytFirstLine is big enough for two lines of info and use two ArraySegments.
                        // Or, if it's not long enough, I could make one read from 0 to 2*intLineLength, cut back into the same code, 
                        // and segement that.
                        //
                        // I'm going to painfully and stodgily read in the second line from the original stream.
                        FileStream fs2 = File.Open(this.strTableFileLoc, FileMode.Open);
                        byte[] abytSecondLine = Utils.ReadToLength(fs2, intLineLength, intLineLength);   // the reader is zero-based, so start at the exact length position.
                        fs2.Close();    // TODO: Same thing as above -- keep open or, better yet, read from previously opened fs.

                        int j = 1;  // use 1 for j initially so we skip the line's initial 0x11
                        Queue<Column> qColumns = new Queue<Column>();
                        while (j < intLineLength)
                        {
                            Column column = new Column();
                            column.strColName = "";

                            // I think the for loop with breaks is more readable, so I'm going to avoid while (which is what I used initially)
                            // though it might make more sense to while loop until 0x00 or 0x11, and if it doesn't work correctly, the thrown exception 
                            // for overshooting the length makes some sense, as the row was misconstructed.
                            int k;  // scoped to check it later.
                            for (k = j; k < intLineLength; k++)   // use 1 for k so we skip the line's initial 0x11
                            {
                                if (0x00 == abytSecondLine[k] || 0x11 == abytSecondLine[k])
                                    break;  // end of the column information
                                else
                                    column.strColName += (char)abytSecondLine[k];
                            }
                            column.isFuzzyName = 0x00 != abytSecondLine[k]; // if we were forced to end early (0x11 "end of col", not 0x00 "end of name"), we've got a fuzzy name.  See above.

                            column.intColStart = j; // start is at the first byte of the entry; this col starts on the jth bit of each row.
                            column.colType = (COLUMN_TYPES)abytFirstLine[j++];

                            if (Column.IsSingleByteType(column.colType))
                            {
                                // handle one byte col types
                                column.intColLength = 1;
                            }
                            else
                            {
                                Stack<byte> stackLength = new Stack<byte>();
                                while (abytFirstLine[j] != 0x00 && abytSecondLine[j] != 0x11)
                                {
                                    stackLength.Push(abytFirstLine[j++]);
                                }

                                if (COLUMN_TYPES.AUTOINCREMENT == column.colType)
                                {
                                    // We're using the intColLength to store the last autoincrement
                                    // value in this column type.
                                    column.intColLength = 4;    // NOTE: Changing from INT(4) will bork things.
                                    column.intAutoIncrementCount = Utils.ByteArrayToInt(stackLength.ToArray());
                                }
                                else
                                {
                                    column.intColLength = Utils.ByteArrayToInt(stackLength.ToArray());
                                }
                            }

                            // Roll to the end of the column's space.
                            while (abytSecondLine[j] != 0x11)
                            {
                                j++;
                            }

                            column.intColIndex = qColumns.Count;    // 0-based index of this column, if they're taken in order.
                            qColumns.Enqueue(column);

                            if (0x11 == abytFirstLine[j + 1])
                            {
                                break;  // if we found two 0x11s in a row, stop.  End of the row.
                            }
                            else
                            {
                                j++; // skip the col end 0x11 and get ready for the next column
                            }

                        }   // loop back to get next -- while (j < intLineLength)

                        _columns = qColumns.ToArray();

                        // +1 once for the check, above for the second 0x11 at abytFirstLine[j+1], 
                        // then another because we want length, not 0-based byte number in the array.
                        // Note that this should include all the 0x11 overhead.
                        _intRowLength = j + 1 + 1;  

                    }   // eo bFoundLineEnd == true logic.
                }   // eo while (!bFoundLineEnd)
            }   // eo else for if (_strTableName.Equals("") || _strTableParentFolder.Equals("")) // "initialization check"
        }

        public COLUMN_TYPES getColType(string strColName)
        {
            return this.getColType(this._colIndexFromName(strColName));
        }

        public COLUMN_TYPES getColType(int intColIndex)
        {
            COLUMN_TYPES colType = (COLUMN_TYPES)Activator.CreateInstance(typeof (COLUMN_TYPES)); // seems to be a slick way to get "0";
            // the default val for any enum
            // see...
            // http://stackoverflow.com/a/8927369/1028230
            // http://stackoverflow.com/a/529937/1028230  -- "The default for an enum (in fact, any value type) is 0 -- even if that is not a valid value for that enum. It cannot be changed."

            colType = this._columns[intColIndex].colType;
            
            return colType;
        }

        public Column[] getColumns()    {
            return (Column[])_columns.Clone();
        }

        /// <summary>
        /// Returns null if no name found.
        /// </summary>
        /// <param name="strColName">Column name to find; checks fuzzy matches as well.</param>
        /// <returns>null if column name isn't found</returns>
        public Column getColumnByName(string strColName)
        {
            Column colReturn = null;

            foreach (Column colTemp in _columns)
            {
                // TODO: Figure out StringComparison a little better.
                // http://msdn.microsoft.com/en-us/library/system.stringcomparison.aspx
                if (colTemp.strColName.Equals(strColName, StringComparison.InvariantCultureIgnoreCase) 
                    || (colTemp.isFuzzyName && strColName.ToLower().StartsWith(colTemp.strColName.ToLower())))
                {
                    colReturn = colTemp;
                    break;
                }
            }

            return colReturn;
        }

        public bool containsColumn(string strColName)
        {
            return this.getRawColName(strColName) != null;
        }

        public string getRawColName(string strColName)
        {
            string strReturn = null;
            StringComparison caseSensitive = StringComparison.CurrentCultureIgnoreCase;
            if (!this.bIgnoreColNameCase)
            {
                caseSensitive = StringComparison.CurrentCulture;
            }

            // TODO: Handle casing more deliberately.  This is hacky.
            foreach (Column colTemp in _columns)    {
                if ( colTemp.strColName.Equals(strColName, caseSensitive)
                    || (colTemp.isFuzzyName && strColName.StartsWith(colTemp.strColName))
                    || (this.bIgnoreColNameCase && colTemp.isFuzzyName && strColName.ToLower().StartsWith(colTemp.strColName.ToLower()))
                )
                {
                    strReturn = colTemp.strColName;
                    break;
                }
            }

            return strReturn;
        }

        public void writeRow(byte[] abytRow)    {
            FileStream stream = new FileStream(this.strTableFileLoc, FileMode.Append);
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(abytRow);
            writer.Flush();
            writer.Close();
        }

        public void writeMetadataRowsAndPrepareNewTable(List<byte> lstColMetadata, List<byte> lstColNames, string strTableName, string strDbLoc)
        {
            this.prePrepareNewTable(strDbLoc, strTableName);  // hacky. we already had the table name.
            _writePredfinedRow(lstColMetadata);
            _writePredfinedRow(lstColNames);
            _prepareTableFromFile();
        }

        private void _writePredfinedRow(List<byte> lstBytToWrite) {
            using (BinaryWriter b = new BinaryWriter(File.Open(this.strTableFileLoc, FileMode.Append)))
            {
                b.Write(lstBytToWrite.ToArray());
                b.Write(new byte[] { 0x11 }); // I'm going to end the row with two 11s.  
                // Because I'm writing these first two rows, I can guarantee there won't
                // be any 0x11's in them except where I need them to be.  And since I only
                // need to find the end of the first row to find each row's length, I'mm
                // golden.
            }
        }

        // was going to use LINQ, but that requires an extra reference.
        // though, admittedly, I'll probably add it later.
        private int _colIndexFromName(string strColName)    {
            int intColIndex = -1;

            for (int i = 0; i < this._columns.Length; i++)
            {
                if (this._columns[i].strColName.Equals(strColName))
                {
                    intColIndex = i;
                }
            }

            if (-1 == intColIndex) {
                throw new Exception("Invalid Column Name"); // TODO: Not sure this should throw an error, but also not sure what a good default val on fail would be that's not confusing.
            }

            return intColIndex;
        }

        /// <summary>
        /// I'm not a huge fan of the way I'm doing this, but this is basically
        /// to change the autoincrement count for a column, which is being stored
        /// in what's normally the column length area.  We're capping autoincrements
        /// at 4 byte longs (painfully minus one byte for column type, thus the check
        /// against 256^3 = 16777216).
        /// </summary>
        /// <param name="strColName"></param>
        /// <param name="intNewLenth"></param>
        public void updateAutoIncrementCount(string strColName, int intNewCount)
        {
            Column colAutoInc = this.getColumnByName(strColName);

            if (null == colAutoInc || colAutoInc.colType != COLUMN_TYPES.AUTOINCREMENT)
            {
                throw new Exception("Autoincrement column " + strColName + " not found.");
            }

            if (intNewCount >= 16777216)
            {
                throw new Exception("Autoincrement overflow (2).  Congratulations.  Column: " + strColName);
            }

            byte[] abytLength = Utils.IntToByteArray(intNewCount);
            using (BinaryWriter b = new BinaryWriter(File.Open(this.strTableFileLoc, FileMode.Open)))
            {
                b.Seek(colAutoInc.intColStart+1, SeekOrigin.Begin);     // +1 to move past the colType marker.
                b.Write(abytLength);
            }
        }

        public static void Main(string[] args)
        {
            TableContext TableContext = new TableContext();
            TableContext.prePrepareNewTable(MainClass.cstrDbDir, "TableContextTest");

            //if (false)  // if that file doesn't already exist/run only the first time.
            //{
            //    List<byte> lstMetadataRow = new List<byte>(new byte[] { 0x11, 0x03, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x11, 0x02, 0x05, 0x00, 0x00, 0x00, 0x11, 0x02, 0x02, 0x11, 0x04, 0x05, 0x00, 0x00, 0x00, 0x11, 0x04, 0x05, 0x00, 0x00, 0x00, 0x11 });
            //    List<byte> lstColNames = new List<byte>(new byte[] { 0x11, 0x49, 0x44, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x11, 0x43, 0x49, 0x54, 0x59, 0x00, 0x11, 0x53, 0x54, 0x11, 0x4C, 0x41, 0x54, 0x5F, 0x4E, 0x11, 0x4C, 0x4F, 0x4E, 0x47, 0x5F, 0x11 });

            //    TableContext.writeMetadataRowsAndPrepareNewTable(lstMetadataRow, lstColNames);
            //}

            TableContext._prepareTableFromFile();

            Column[] columns = TableContext.getColumns();

            Console.WriteLine("Press return to end");
            Console.Read();
        }
    }
}
