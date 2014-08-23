# Moore's Database (SqlDb#) #

---

### License [(full text at this link)](http://mozilla.org/MPL/2.0/)

    // =========================== LICENSE ===============================
    // This Source Code Form is subject to the terms of the Mozilla Public
    // License, v. 2.0. If a copy of the MPL was not distributed with this
    // file, You can obtain one at http://mozilla.org/MPL/2.0/.
    // ======================== EO LICENSE ===============================


### VERSION 0.0.3.4: Nothing is guaranteed to work.  NOTHING.  Use at your own risk.

### TODONEs:
1. UPDATE statements.
2. Port to Windows Phone 8 -- download [here](https://github.com/ruffin--/SqlDbSharp/blob/master/bin/SqlDbSharpWP8.dll?raw=true).
3. Better support for [wwwsqldesigner](http://code.google.com/p/wwwsqldesigner/) generated SQL.
4. Extended character set support/UTF-8 encoding.

Smaller updates:

1. UPDATEs using other columns for values.
2. SELECT MAX trivially supported.


### Major TODOs:
1. Tests.
2. Docs.
3. Code review.
4. ORs in WHERE clauses.
5. Select subset of columns from second table of INNER JOINs.

###Usage

**Simple Usage**

Step 1. Build the dll using the master branch for Mono or conventional .NET.  Build from the WP8 branch or download the Windows Phone class library [here](https://github.com/ruffin--/SqlDbSharp/blob/master/bin/SqlDbSharpWP8.dll?raw=true) for WP8.

Step 2: Use the code below *at your own risk*.

    DatabaseContext database = new DatabaseContext(@"C:\temp\DatabaseName"); // or Mac path, etc.
    CommandParser parser = new CommandParser(database);
    
    string sql = @"CREATE TABLE TestTable (
            ID INTEGER (4) AUTOINCREMENT,
            CITY CHAR (10));";
    parser.executeCommand(sql);
    parser.executeCommand("INSERT INTO TestTable (CITY) VALUES ('New York');");
    parser.executeCommand("INSERT INTO TestTable (CITY) VALUES ('Boston');");
    parser.executeCommand("INSERT INTO TestTable (CITY) VALUES ('Fuquay');");
    
    sql = "SELECT * FROM TestTable;";
    DataTable dataTable = (DataTable)parser.executeCommand(sql);
    Console.WriteLine(InfrastructureUtils.dataTableToString(dataTable));
    parser.executeCommand("DROP TABLE TestTable;");

---

## SqlDb#

### What?

SqlDb#'s goal is to be a simplest-case, embeddable, crossplaform, YesSQL datastore written in C#. Its ongoing emphasis is on a laconic, yet easy to understand and maintain, codebase that enables the serialization of data to files using a select subset of ANSI SQL.

Using SQL has many benefits over using a custom-rolled text file as an application's datastore, facilitating both initial coding as well as changing to another, more fully-featured database when a project requires it.  **At this early stage, SqlDb# is best used for crossplatform application prototyping.**  It has been tested on .NET 4.0, Windows Phone 8, and Xamarin's Mono.  

The [master branch](https://github.com/ruffin--/SqlDbSharp/tree/master) should compile without edit for Mono and conventional .NET.  A quick and dirty port to Windows Phone 8 can be found in the [WP8 branch](https://github.com/ruffin--/SqlDbSharp/tree/WP8), and a prebuilt dll for WP8 can be downloaded [here](https://github.com/ruffin--/SqlDbSharp/blob/master/bin/SqlDbSharpWP8.dll?raw=true). Note that the WP8 branch should work fine on the other target branches with a single line change currently, the major difference being the use of a [shim for the dbms' System.Data usage](https://github.com/ruffin--/DataTableWP8).


### Warnings!

SqlDb# is a minimally viable product.  It does not currently support the creation of keys, constraints, searching with LIKE and "%" wildcards, NOTs, JOINs beyond the simplest INNER JOINs, INSERTING more than one row in a single statement, any in-database functions (like CONCAT or MAX), TRIGGERs, OR in a WHERE clause (though IN works), SELECT INTO, or many datatypes.  **SqlDb# is surprisingly idiosyncratic.**  Please read through the [Idiosyncracies.md](https://github.com/ruffin--/SqlDbSharp/blob/master/docs/idiosyncrasies.md) file in the docs folder.

Though its internal structure is designed to make it easy to visualize tables as raw hexadecimal values, the same structure makes column naming, well, strange.  Please read the documentation about "fuzzy" naming closely.  Additionally, some seemingly legitimate column lengths are not allowed.  **The previous paragraph may have undersold the project's [idiosyncrasies](https://github.com/ruffin--/SqlDbSharp/blob/master/docs/idiosyncrasies.md).**

This project was developed under the code name "Moore's Database", which should help explain what else is missing from SqlDb#: Speed.  Too slow?  SqlDb# should [become approximately twice as fast every two years](http://en.wikipedia.org/wiki/Moore's_law), though that law is apparently losing steam.  Indices could be added later, but such a package would not necessarily be a part of the core project. 

To help ensure crossplatform usability, it has been written on both Visual Studio on Windows and MonoDevelop on Mac OS X, and ported and tested on Windows Phone 8.


### Why? 

It should be easy to prototype crossplatform C# code without wasting time installing database engines, and, in many cases, without having to install different ones for each platform.

SqlDb# was written largely because of setup hurdles encountered attempting to deploy other obvious embedded database solutions for C# crossplatform.  [SQL Server Express](http://msdn.microsoft.com/en-us/library/vstudio/ms233763.aspx) is not crossplatform.  [SQLite](http://www.sqlite.org/) must be installed on Windows and has a few [nontrivial setup requirements on OS X](http://stackoverflow.com/questions/8090152/), both of which create a clear barrier to entry out of proportion for applications with modest data serialization needs. [C#-SQLite](https://code.google.com/p/csharp-sqlite/) is an interesting alternative, but [still had issues](https://groups.google.com/forum/#!topic/csharp-sqlite/osjfgLikIkU) when I tried it out in May of 2012.

There's no reason for a search for a datastore library or dll usage limitations to slow the prototyping of useful, crossplatform C# applications.

### License?

This package is offered under the [Mozilla Public License (MPL)](http://mozilla.org/MPL/2.0/), following [GNG's not GNU](http://myfreakinname.blogspot.com/2002/05/gng-manifesto.html) (GNG's) guidelines, as it is intended to be used not just as a compiled library but, when useful, in code format as part of an otherwise independent solution of files.  The MPL allows this package's use without being forced to reference a precompiled dll.

**NOTE:** Regardless of any stated intention, strict adherence to the MPL is required. 

### Utilities?

As stated before, SqlDb#'s internal structure is designed to make it easy to visualize tables as raw hexadecimal values.  A very simple reader/visualizer is included in the SqlDb# repository or can be downloaded separately for [Windows](http://rufwork.com/code/SqlDbSharp/HexReader.zip) or <a style="color:orange" href="http://rufwork.com/code/SqlDbSharp/HexReaderMac.zip">Mac</a>. Mac version requires Mono, run the following from the command line: `mono HexReader.exe`

<img src="http://rufwork.com/code/SqlDbSharp/HexReaderScreenshot.png"><br>

And the nasty Windows.Forms compile to OS X:

<center><img src="http://rufwork.com/code/SqlDbSharp/HexReaderScreenshotMac.png"></center><br>

Note the border around each column, made with 0x11's.  The first row starts with column type (index value from 0-255) followed by the column length, except in the case of autoincrement columns.  The second row includes as many characters of the column's name as can fit in the length of the column.  In the third column of the screenshot, there is a CHAR column named "STATE" with a length of 2, and only "ST" is displayed.  The final column is DATETIME, which is a decimal value of Ticks serialized, taking eight bytes of length.  Its name is "TIMESTAMP", but only the first eight characters are displayed -- "TIMESTAM".  Deleted lines are written over with 88s, currently not cleaned.

## Liability

The creator(s) of this package assume no liability for its use.  **USE THIS AND ANY ACCOMPANYING CODE AT YOUR OWN RISK.** 

Below contents do not constitute the entirety of the license. These sections are reprinted only to highlight their contents. Please read the entire license file ([LICENSE](http://mozilla.org/MPL/2.0/) is included with package contents) and understand that your use indicates your acceptance of this license before using this package.

    ************************************************************************
    *                                                                      *
    *  6. Disclaimer of Warranty                                           *
    *  -------------------------                                           *
    *                                                                      *
    *  Covered Software is provided under this License on an "as is"       *
    *  basis, without warranty of any kind, either expressed, implied, or  *
    *  statutory, including, without limitation, warranties that the       *
    *  Covered Software is free of defects, merchantable, fit for a        *
    *  particular purpose or non-infringing. The entire risk as to the     *
    *  quality and performance of the Covered Software is with You.        *
    *  Should any Covered Software prove defective in any respect, You     *
    *  (not any Contributor) assume the cost of any necessary servicing,   *
    *  repair, or correction. This disclaimer of warranty constitutes an   *
    *  essential part of this License. No use of any Covered Software is   *
    *  authorized under this License except under this disclaimer.         *
    *                                                                      *
    ************************************************************************

    ************************************************************************
    *                                                                      *
    *  7. Limitation of Liability                                          *
    *  --------------------------                                          *
    *                                                                      *
    *  Under no circumstances and under no legal theory, whether tort      *
    *  (including negligence), contract, or otherwise, shall any           *
    *  Contributor, or anyone who distributes Covered Software as          *
    *  permitted above, be liable to You for any direct, indirect,         *
    *  special, incidental, or consequential damages of any character      *
    *  including, without limitation, damages for lost profits, loss of    *
    *  goodwill, work stoppage, computer failure or malfunction, or any    *
    *  and all other commercial damages or losses, even if such party      *
    *  shall have been informed of the possibility of such damages. This   *
    *  limitation of liability shall not apply to liability for death or   *
    *  personal injury resulting from such party's negligence to the       *
    *  extent applicable law prohibits such limitation. Some               *
    *  jurisdictions do not allow the exclusion or limitation of           *
    *  incidental or consequential damages, so this exclusion and          *
    *  limitation may not apply to You.                                    *
    *                                                                      *
    ************************************************************************



## [GNG Manifesto](http://myfreakinname.blogspot.com/2002/05/gng-manifesto.html) ##

People have a right to make their own code for any purpose.

If a freely offered, open source package is essentially used as a self-contained library by new code, it is presumptuous to force new code built atop or alongside of this package to fit any particular political ideals. When I buy paper, my writings are not and should not be limited to the ideologies of the paper mill's owner. Though my words are meaningless to a reader without the paper, and a written paper with its paper removed would be nothing, no one believes that I am nor should I be beholden to the paper's creators' ideals and views for using it.

At the same time, if someone takes freely offered code and changes that code's internal workings to behave in a similar but arguably improved fashion, they have an ethical obligation to the package's original contributors to not only offer this newly updated source freely to anyone interested, but also to contact the original project's maintainer(s), if they can be found, and let them know of and provide for them those changes. If I were to change one word or several of another person's written speech, I certainly could not, ethically, present it as my own and use it solely for my own gain. To alert the original author(s), or, at the very least, my listeners, of my changes and to my use of another author's words is not only a courtesy but an ethical obligation.

This is why the GPL is discouraged and the use of the LGPL (Lesser General Public License) for open source projects is recommended in all but two cases.  The first is if it is your intent to allow the *files* of your code to live next to those of proprietary code, for both to comingle as source in a single project.  The second is when a library is written in Java, as the LGPL may have an unexpected aftereffect when used with Java.  In the first, the MPL (Mozilla Public License) will require changes to Free code to be returned to its community but allow for code files to mix freely with code of any license.  In the second case, it is best to release with an LGPL-style license you authored that covers Java -- or the MPL, if appropriate or expedient -- to avoid undue restriction.

This is also why I dislike the X11, BSD, and MIT licenses. *These licenses don't do enough to protect the contributions of the people that made the code* -- they essentially enable legalized plagiarism. It's certainly one's right to make code that's this unregulated, but these licenses are nearly overly altruistic motivations.

---
#####This may be the longest README.md available on GitHub, at no extra charge. 

