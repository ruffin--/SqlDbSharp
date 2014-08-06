// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.IO;
using System.Data;

using org.rufwork;
using org.rufwork.mooresDb;
using org.rufwork.mooresDb.infrastructure;
using org.rufwork.mooresDb.infrastructure.commands;
using org.rufwork.mooresDb.infrastructure.contexts;
using org.rufwork.mooresDb.infrastructure.serializers;

public class MainClass
{
    // Right now, on Windows, that's "C:\\Users\\YourUserName\\Documents\\MooresDbPlay"
    public static readonly string cstrDbDir = Utils.cstrHomeDir + Path.DirectorySeparatorChar + "MooresDbPlay";
    public static bool bDebug = false;
    public static string buildData = "20140607";    // not always incremented with each build.
    public static string version = "0.0.3.2";

    public static void Main (string[] args)
    {
        MainClass.TestDb();

        Console.WriteLine("Return to quit.");
        Console.Read();
    }

    public static void TestBinWrites()    {
        string strFileLoc = MainClass.cstrDbDir + Path.DirectorySeparatorChar + "test.db";
        
        Directory.CreateDirectory(MainClass.cstrDbDir);
        
        Console.WriteLine(strFileLoc);
        WriteDefaultValues(strFileLoc);
        DisplayValues(strFileLoc);

    }

    public static void TestDb()  {
        try
        {
            string strTableName = DateTime.Now.Ticks.ToString();
            Console.WriteLine ("Testing with table named: " + strTableName);

            DatabaseContext database = new DatabaseContext(MainClass.cstrDbDir);

            //=======================================
            // Create
            //=======================================
            string strSql = @"create TABLE " + strTableName + @"
                (ID INTEGER (4) PRIMARY KEY,
                CITY CHAR (35),
                STATE CHAR (2),
                LAT_N REAL (5),
                LONG_W REAL (5));";
            Console.WriteLine(strSql);
            
            CreateTableCommand createTableCommand = new CreateTableCommand(database);
            createTableCommand.executeStatement(strSql);


            //=======================================
            // Insert
            //=======================================
            //TableContext = new TableContext(MainClass.cstrDbDir, strTableName);
            strSql = @"INSERT INTO " + strTableName + @" (ID, CITY, STATE, LAT_N, LONG_W,)
                                VALUES (3, 'Chucktown', 'SC', 32.776, -79.931);";
            Console.WriteLine(strSql);

            InsertCommand _insertCommand = new InsertCommand(database);
            _insertCommand.executeInsert(strSql);

            //=======================================
            // Select
            //=======================================
            strSql = "SELECT ID, CITY, LONG_W, LAT_N FROM " + strTableName + @" WHERE city = 'Chucktown' AND LAT_N > 32";
            Console.WriteLine(strSql);

            SelectCommand selectCommand = new SelectCommand(database);
            DataTable dtOut = selectCommand.executeStatement(strSql);

            // Not getting any rows right now.
            foreach (DataRow dr in dtOut.Rows) 
            {
                foreach (DataColumn dc in dtOut.Columns)
                {
                    Console.WriteLine(dc.ColumnName + ": #" + dr[dc].ToString() + "#");
                }
            }

        }
        catch (Exception e)
        {
            Console.WriteLine("issue in Main's TestDb: " + System.Environment.NewLine 
                + e.ToString());
        }
    }

    public static void WriteDefaultValues(string strFileName)
    {


        using (BinaryWriter writer = new BinaryWriter(File.Open(strFileName, FileMode.Create)))
        {
            writer.Write(0x11);
            writer.Write(1.250F);
            writer.Write(0x11);
            writer.Write(System.Text.Encoding.ASCII.GetBytes ( System.IO.Path.Combine (Utils.cstrHomeDir, "tempFile.temp")) );
            writer.Write(0x11);
            writer.Write(true);
            writer.Write(0x11);
        }
    }
        
    public static void DisplayValues(string fileName)
    {
        float aspectRatio;
        string tempDirectory;
        int autoSaveTime;
        bool showStatusBar;
            
        if (File.Exists(fileName))
        {
            using (BinaryReader reader = new BinaryReader(File.Open(fileName, FileMode.Open)))
            {
                aspectRatio = reader.ReadSingle();
                tempDirectory = reader.ReadString();
                autoSaveTime = reader.ReadInt32();
                showStatusBar = reader.ReadBoolean();
            }
                
            Console.WriteLine("Aspect ratio set to: " + aspectRatio);
            Console.WriteLine("Temp directory is: " + tempDirectory);
            Console.WriteLine("Auto save time set to: " + autoSaveTime);
            Console.WriteLine("Show status bar: " + showStatusBar);
        }
    }
}
