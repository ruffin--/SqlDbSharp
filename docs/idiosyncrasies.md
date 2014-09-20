##Hardly exhaustive list of idiosyncrasies

Look, this is a really minimally viable product, with an emphasis on the "minimal".  The command parser is not particularly defensively coded.  If you don't follow its rules, NO SOUP FOR YOU.

Here are some major examples:

1. **"Fuzzy" column naming is bat-crazy.**
    * Names are only serialized to the length of the column.
    * Columns whose names are as many characters as the column or longer are considered "fuzzy named" columns.
    * For example, since INTs are four bytes long, and AUTOINCREMENT columns must be INTs (see below), all AUTOINCREMENT columns' names are only stored to four characters.  Any AUTOINCREMENT column whose name is four or more characters long is fuzzily named.
    * Names longer than the length of the characters stored for its column in the database can be used to access fuzzy named columns in SELECT statements.  See the below example.
        * CREATE TABLE Table1 (ThisHasALongName INTEGER (4) AUTOINCREMENT);
        * The below statements are all valid SELECTs against the above table.
            * SELECT This FROM Table1;
            * SELECT ThisHasALongName FROM Table1;
            * SELECT ThisPretzelIsMakingMeThirsty FROM Table1;
        * Note that DataTables returned from SELECT statements will have Columns named whatever the SQL called them, so This, ThisHasALongName, and ThisPretzelIsMakingMeThirsty for the three statements, above, respectively.  
2. **All escaped single quotes/apostrophes in SQL are turned into [grave accents](http://en.wikipedia.org/wiki/Grave_accent).**  
    * INSERT INTO Table (StringVal) VALUES ('Let''s go to the store!');
    * Will have the string value "Let`s go to the store!" inserted into the database.
    * WHEREs can use doubled single quotes or grave accents to search. 
3. **Dot notation for tables not supported.**
    * The following will *not* retrieve the column ID from Table1.
         * SELECT Table1.ID from Table1;
4. **WHERE clauses in JOINs only apply to the first table in the FROM clause.**
    * SELECT * FROM Table1 INNER JOIN Table2 ON Table1.ID = Table2.Table1Id WHERE ID > 8 AND Table1Id = 7;
        * The AND clause will break.
    * Any normal JOIN will have the following written to the Console: "Note that WHERE clauses are not yet applied to JOINed tables."
5. **Only INNER JOINs that are equijoins are supported.**
    * SELECT * FROM Table1 INNER JOIN Table2 ON Table1.Id = Table2.CityId;
    	* The above line works.
6. **INNER JOIN fields *must* be prefixed by their table name.**
    * SELECT * FROM Table1 INNER JOIN Table2 ON Id = CityId;
    	* That line is no good.
7. **INNER JOIN equivalents in WHERE clauses are not supported.**
    * SELECT * FROM Table1, Table2 WHERE Table1.Id = Table2.CityId;
    	* Doesn't work.
8. **No aliases for fields in statements.**
	* SELECT Name AS BirthName FROM Names; -- No good.
9. **No aliases for tables in statements.**
	* SELECT * FROM jive jiveTown INNER JOIN music GreatMusic on jiveTown.ID = GreatMusic.OriginId; -- Also no good.
10. **AUTOINCREMENT columns must be four byte INTs**
    * ID INTEGER (4) AUTOINCREMENT
11. **Columns of length 4369 aren't allowed**
    * Definitively my new favorite idiosyncratic behavior.
    * Initially, columns this length would cause strange, destructive (?) errors.
    * Now the error: "Idiosyncratically, column lengths of [exactly] 4369 are not allowed." is displayed if you try.
    * The end of the first table metadata row is marked with 0x11 0x11.  Guess what [4369 is in hex](http://lmgtfy.com/?q=4369+in+hexadecimal)?
    * Columns of lengths 4368 and 4370 are fine.
    * 69905 would also be bad, but that's even bat-crazier huge than 4369, which already really shouldn't have happened in practice.  Discuss. 


Enjoy.