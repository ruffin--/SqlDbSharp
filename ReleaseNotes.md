#Release notes:
<br>

##0.0.4.3 - 20150513

* Fields from "main" table not explicitly mentioned in the SELECT are no longer returned by INNER JOINs that require them.
	* For example, `SELECT field1, field2, table2.field1 FROM table1 INNER JOIN table2 ON table1.t2Id = table2.id` will no longer return `table1.t2Id`. 
	* It does, however, still return `table2.id`, which I'll take a look at next (and potentially not increment the version).
	* The method used here is particularly bullheaded, spaghetti-ish, and inane.

##0.0.4.2 - 20150410

* `AS` support for *columns only* added.

##0.0.4.1 - 20140906:

* Fixed bug for `SELECT * FROM Table1 INNER JOIN Table2 ON Table1.LongFuzzyName = Table2.JoinField` -- Long fuzzy wouldn't be in the primary table's DataTable.
* Fixed the name of this file.  Brilliant.

##0.0.4 - 20140906:

* Started compiling release notes.
* Fairly large refactor of INNER JOIN kludge. (See below)
* Starting to replace `Console.Write`s with `SqlDbSharpLogger.LogMessage` so that they're easier to reroute.

**INNER JOIN Changes**

Now allows a subset of SQL compliant statements where before it expected and required non-compliant syntax.

`SELECT * FROM Table1 INNER JOIN Table2 ON Table1.id = Table2.table1Id;`

The above essentially worked before. These didn't:

1. `SELECT table1Col1, table1Col2, Table2.* FROM Table1 INNER JOIN Table2 ON Table1.id = Table2.table1Id;`
2. `SELECT table1Col1, table1Col2, Table2.table2Col1, Table2Col2 FROM Table1 INNER JOIN Table2 ON Table1.id = Table2.table1Id;`


Note, however, that the second will *also* return `Table2.table1Id ` as part of making JOINs a little easier.

Nested joins still do not work. 

##Stuff from earlier updates

1. UPDATE statements.
2. Port to Windows Phone 8 -- download [here](https://github.com/ruffin--/SqlDbSharp/blob/master/bin/SqlDbSharpWP8.dll?raw=true).
3. Better support for [wwwsqldesigner](http://code.google.com/p/wwwsqldesigner/) generated SQL.
4. Extended character set support/UTF-8 encoding.
5. UPDATEs using other columns for values.
6. SELECT MAX trivially supported.
