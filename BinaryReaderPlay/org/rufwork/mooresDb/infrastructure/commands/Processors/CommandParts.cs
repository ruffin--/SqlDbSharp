using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using org.rufwork.mooresDb.infrastructure.contexts;
using org.rufwork.mooresDb.infrastructure.tableParts;
using org.rufwork.extensions;
using org.rufwork.mooresDb.exceptions;
using org.rufwork.utils;
using com.rufwork.utils;

namespace org.rufwork.mooresDb.infrastructure.commands.Processors
{
    // Convenience class to hold different parts of the SELECT
    // statement's text.
    public class CommandParts
    {
        public string strOriginal;

        public string strSelect;
        public string strUpdate;
        public string strFrom;
        public string strWhere;
        public string strOrderBy;
        public string strLimit;
        public string strInnerJoinKludge;
        public Queue<string> qInnerJoinFields = new Queue<string>();
        public Dictionary<string, string> dictFnsAndFields = new Dictionary<string, string>();

        public string strTableName; // TODO: Ob need to go to a collection of some sort

        public Column[] acolInSelect;
        public Queue<string> qstrAllColumnNames = new Queue<string>();
        public List<string> lstrJoinONLYFields = new List<string>();   // Sir Still Not Paricularly Well Named in this Film.
        public Dictionary<string, string> dictUpdateColVals = new Dictionary<string, string>();
        public Dictionary<string, string> dictFuzzyToColNameMappings = new Dictionary<string, string>();
        public Dictionary<string, string> dictRawNamesToASNames = new Dictionary<string, string>();
        public COMMAND_TYPES commandType;

        private DatabaseContext _database;
        private TableContext _tableContext;

        public enum COMMAND_TYPES { SELECT, UPDATE, DELETE, INSERT };

        public CommandParts(DatabaseContext database, TableContext table, string strSql, COMMAND_TYPES commandType)
        {
            _database = database;
            _tableContext = table;

            this.commandType = commandType;
            this.strOriginal = strSql;

            switch (commandType)
            {
                case COMMAND_TYPES.SELECT:
                    _parseSelectStatement(strSql);
                    _findSelectFunctionCalls();
                    _getColumnsToReturn();
                    break;

                case COMMAND_TYPES.UPDATE:
                    _parseUpdateStatement(strSql);
                    break;

                default:
                    throw new Exception("Unhandled statement type in CommandParts");
            }
        }

        public void _parseUpdateStatement(string strSql)
        {
            int intTail = strSql.Length;
            int intIndexOf = -1;
            StringComparison casedCompare = StringComparison.CurrentCultureIgnoreCase;  // TODO: Parsing statements, why would I want cased only?  That is, why would this ever change?

            string[] astrTokens = strSql.Split();
            if (
                    astrTokens.Length < 6
                    || !astrTokens[0].Equals("UPDATE", casedCompare)
                    || !astrTokens[2].Equals("SET", casedCompare)
                    || !astrTokens[4].Equals("=", casedCompare)
                )
            {
                throw new Exception("Invalid UPDATE statement");
            }

            this.strTableName = astrTokens[1];

            intIndexOf = strSql.IndexOf("WHERE", casedCompare);
            if (-1 < intIndexOf)
            {
                this.strWhere = strSql.Substring(intIndexOf, intTail - intIndexOf);
                intTail = intIndexOf;
            }

            intIndexOf = strSql.IndexOf(" SET ", casedCompare) + 5;
            string strVals = strSql.Substring(intIndexOf, intTail - intIndexOf).Trim();

            string[] astrVals = strVals.Split(',');
            foreach (string strVal in astrVals)
            {
                string[] astrColAndVal = strVal.Split('=');
                if (astrColAndVal.Length != 2)
                {
                    throw new Exception("Syntax error: Invalid update SET clause: " + strVal);
                }
                this.dictUpdateColVals.Add(astrColAndVal[0].Trim(), astrColAndVal[1].Trim());
            }
        }

        private void _parseSelectStatement(string strSql)
        {
            int intTail = strSql.Length;
            int intIndexOf = -1;

            if (!strSql.ToLower().StartsWith("select") || !strSql.ToLower().Contains("from"))
            {
                throw new Exception("Invalid SELECT statement");
            }

            // TODO: Need some way to be sure what we're not false hitting
            // on a table named NOLIMITHOLDEM or whatever.
            intIndexOf = strSql.LastIndexOf("LIMIT", StringComparison.CurrentCultureIgnoreCase);
            if (-1 < intIndexOf)
            {
                this.strLimit = strSql.Substring(intIndexOf, intTail - intIndexOf).FlattenWhitespace();
                intTail = intIndexOf;
            }

            intIndexOf = strSql.LastIndexOf("ORDER BY", StringComparison.CurrentCultureIgnoreCase);
            if (-1 < intIndexOf)
            {
                this.strOrderBy = strSql.Substring(intIndexOf, intTail - intIndexOf).FlattenWhitespace();
                intTail = intIndexOf;
            }

            intIndexOf = strSql.IndexOf("WHERE", StringComparison.CurrentCultureIgnoreCase);
            if (-1 < intIndexOf)
            {
                this.strWhere = strSql.Substring(intIndexOf, intTail - intIndexOf);
                intTail = intIndexOf;
            }

            intIndexOf = strSql.IndexOf("FROM", StringComparison.CurrentCultureIgnoreCase);
            if (-1 < intIndexOf)
            {
                this.strFrom = strSql.Substring(intIndexOf, intTail - intIndexOf);

                // Look for inner join.
                // TODO: Another reserved word that we don't really want a table to be named: ("join").
                this.strInnerJoinKludge = "";
                if (this.strFrom.IndexOf(" join ", StringComparison.CurrentCultureIgnoreCase) > -1)
                {
                    int intInnerJoin = this.strFrom.IndexOf("inner join", StringComparison.CurrentCultureIgnoreCase);
                    if (intInnerJoin > -1)
                    {
                        this.strInnerJoinKludge = this.strFrom.Substring(intInnerJoin);
                        this.strFrom = this.strFrom.Substring(0, intInnerJoin);

                        // Keep track of the join fields so we can intelligently select but
                        // not display them if they are/aren't in the SELECT.
                        // Let's start with the bullheaded way.

                        // The most natural place to find fields used to join "secondary
                        // tables" (any table after the first in the FROM list) would
                        // actually be in _processInnerJoin in SelectCommand, but this is
                        // already spaghettied enough. So let's dupe some logic and do it
                        // here.
                        // TODO: Consider deciphering lists of tables and fields in a 
                        // refactored CommandParts and removing that from SelectCommand, etc.
                        string strMainTableName = this.strFrom.Substring(4).Trim();
                        string[] innerKludgeTokenized = this.strInnerJoinKludge.StringToNonWhitespaceTokens2();
                        Queue<string> qSecondaryTableNames = new Queue<string>();

                        for (int i=0; i<innerKludgeTokenized.Length; i++)
                        {
                            string toke = innerKludgeTokenized[i];
                            if (toke.ToUpper().StartsWith(strMainTableName.ToUpper()))
                            {
                                this.lstrJoinONLYFields.Add(toke.ReplaceCaseInsensitiveFind(strMainTableName + ".", ""));
                            }
                            else if (qSecondaryTableNames.Any(s => toke.ToUpper().StartsWith(s.ToUpper() + ".")))   // TODO: this kinda suggests "." can't be in a table or column name either. Don't think we're checking that.
                            {
                                this.lstrJoinONLYFields.Add(toke);
                            }

                            // TODO: This makes JOIN a pretty hard keyword. I think that's safe, though.
                            // If you want a table named JOIN, it'll have to be in brackets, right?
                            if (toke.Equals("JOIN", StringComparison.CurrentCultureIgnoreCase))
                            {
                                qSecondaryTableNames.Enqueue(innerKludgeTokenized[i + 1]);
                            }
                        }
                    }
                    else
                    {
                        throw new SyntaxException("Statement includes `join` keyword. Currently, only inner joins are supported.");
                    }
                }

                this.strTableName = this.strFrom.Split()[1];
                _tableContext = _database.getTableByName(this.strTableName);
                if (null == _tableContext)
                {
                    throw new Exception("Table does not exist: " + this.strTableName);
                }
                intTail = intIndexOf;
            }

            this.strSelect = strSql.Substring(0, intTail);
        }

        // TODO: IF you keep this convention, you need to kick out errors if 
        // the function "abbreviations" are in the original SQL.
        private void _findSelectFunctionCalls()
        {
            try
            {
                string[] functionNames = { "MAX", "COUNT" };
                foreach (string strFn in functionNames)
                {
                    string strFnParens = strFn + "(";
                    if (this.strSelect.ToUpper().Contains(strFnParens))
                    {
                        int intFnLoc = this.strSelect.IndexOf(strFnParens, StringComparison.CurrentCultureIgnoreCase);
                        string before = this.strSelect.Substring(0, intFnLoc);
                        int intOpenParen = intFnLoc + strFnParens.Length;
                        int intCloseParen = this.strSelect.IndexOf(')', intFnLoc);
                        string after = this.strSelect.Substring(intCloseParen).TrimStart(')');
                        string middle = this.strSelect.Substring(intOpenParen, intCloseParen - intOpenParen);

                        this.dictFnsAndFields.Add(strFn, middle);
                        this.strSelect = before + middle + after;
                    }
                }
            }
            catch (Exception e)
            {
                ErrHand.LogErr(e, "_findFunctionCalls", "Illegal function call: " + this.strSelect);
            }
        }

        private void _getColumnsToReturn()
        {
            Queue<Column> qCols = new Queue<Column>();

            List<string> lstrCmdTokens = this.strSelect.StringToNonWhitespaceTokens2().Skip(1).ToList();   // Skip 1 to ignore SELECT.

            #region INNER JOIN logic.
            if (!string.IsNullOrEmpty(this.strInnerJoinKludge))
            {
                // TODO: Clean this kludge to get in INNER JOIN fields into datatable
                // while selecting, but to remove these once we're done before
                // returning the DataTable.
                // NOTE: I'm not taking into account fuzzily matching names, in part
                // because I'm planning to remove that painful feature.
                // TODO: I don't think you're really paying attention to aliased columns
                // either, though it might not matter in this case. Still, pay more
                // attention to AS'd columns in case I've missed something.
                foreach (string strSelectCols in lstrCmdTokens.Where(s => !s.ToUpper().Equals("AS")))
                {
                    if (strSelectCols.Equals("*"))
                    {
                        if (1 == lstrCmdTokens.Count())
                        {
                            // If we're selecting * from everything, then there are no 
                            // join-only fields/columns.
                            this.lstrJoinONLYFields = new List<string>();
                        }
                        else
                        {
                            // Else we're selecting everything from the main table.
                            this.lstrJoinONLYFields.RemoveAll(s => !s.Contains("."));
                        }
                    }
                    else if (strSelectCols.Contains("*"))
                    {
                        // Find what table we're removing jive from, then find all cols
                        // that start with that prefix.
                        // TODO: Double check if we every drop requirement to prefix join
                        // columns with table names, though I don't think we will.
                        // TODO: This all goes to heck when we alias JOIN table names. Idiot.
                        string str2ndaryTable = strSelectCols.ReplaceCaseInsensitiveFind(".*", "");
                        this.lstrJoinONLYFields.RemoveAll(s => s.StartsWith(str2ndaryTable + "."));
                    }
                    else if (this.lstrJoinONLYFields.Contains(strSelectCols))
                    {
                        this.lstrJoinONLYFields.Remove(strSelectCols);
                    }
                }

                foreach (string strJoinColName in this.lstrJoinONLYFields)
                {
                    lstrCmdTokens.Add(strJoinColName);
                }
            }   // end kludge for grafting main table join fields not explicitly in SELECT list.
            #endregion INNER JOIN logic.

            string[] astrCmdTokens = lstrCmdTokens.ToArray();

            for (int i = 0; i < astrCmdTokens.Length; i++)
            {
                // TODO: Handle * check with regexp or something. .Equals (in place of .Contains) is legit here if trimmed in StringToNonWhitespaceTokens2, yes?
                // TODO: Do this more intelligently, perhaps looking up likely tables for non-ambiguous column names, then treating every column as an explicit member
                //      of a certain table.
                if (!astrCmdTokens[i].EndsWith("*"))
                {
                    qstrAllColumnNames.EnqueueIfNotContainsCaseInsensitive(astrCmdTokens[i]);

                    #region doesn't end with *
                    if (astrCmdTokens[i].Contains('.'))
                    {
                        // again writing offensive parsing code.
                        string[] astrTableAndColNames = astrCmdTokens[i].Split('.');
                        if (astrTableAndColNames[0].Equals(_tableContext.strTableName) && !qCols.Any(col => col.strColName.Equals(astrCmdTokens[1])))
                        {
                            qCols.Enqueue(_tableContext.getColumnByName(astrTableAndColNames[1]));
                        }
                        else
                        {
                            this.qInnerJoinFields.Enqueue(astrCmdTokens[i]);    // remember these for later when you're adding columns from secondary tables to the datatable for joins.
                        }
                    }
                    else
                    {
                        // TODO: Not normalized.  Frankenstein code.
                        Column colTemp = _tableContext.getColumnByName(astrCmdTokens[i]);
                        if (null != colTemp)
                        {
                            // TODO: Handle repeated names better.
                            if (!qCols.Any(col => col.strColName.Equals(colTemp.strColName)))
                            {
                                qCols.Enqueue(colTemp);
                                if (!colTemp.strColName.Equals(astrCmdTokens[i], StringComparison.CurrentCultureIgnoreCase))
                                {
                                    this.dictFuzzyToColNameMappings.Add(astrCmdTokens[i], colTemp.strColName);
                                }
                            }
                            
                        }
                        else
                        {
                            throw new Exception("SELECT Column does not exist: " + astrCmdTokens[i] + "\n"
                                + "Active statement: " + this.strOriginal);
                        }
                    }
                    #endregion doesn't end with *
                }
                else if (astrCmdTokens[i].Equals("*"))
                {
                    // WET code, as in not DRY, as in "Why Echo This?"
                    foreach (Column column in _tableContext.getColumns())
                    {
                        // For now, we're just not allowing duplicates, which I know stink0rz,
                        // but is probably only horribly useful with real aliases.
                        if (!qCols.Any(col => col.strColName.Equals(column.strColName)))
                        {
                            qCols.Enqueue(column);
                        }
                    }
                }
                else if (astrCmdTokens[i].Contains('.'))
                {
                    // So we only come here if we DO end in `*` and contains a dot. So, eg, `SELECT CustomerName, CustomerId, Items.* FROM Customer INNER JOIN Items on Customer.Id = Items.CustomerId;`
                    if (astrCmdTokens[i].StartsWith(_tableContext.strTableName + "."))
                    {
                        // WET code, as in not DRY, as in "Why Echo This?"
                        foreach (Column column in _tableContext.getColumns())
                        {
                            // For now, we're just not allowing duplicates, which I know stink0rz,
                            // but is probably only horribly useful with real aliases.
                            if (!qCols.Any(col => col.strColName.Equals(column.strColName)))
                            {
                                qCols.Enqueue(column);
                            }
                        }
                    }
                    else
                    {
                        this.qInnerJoinFields.Enqueue(astrCmdTokens[i]);    // remember these for later when you're adding columns from secondary tables to the datatable for joins.
                    }
                }

                if (i + 1 < astrCmdTokens.Length && astrCmdTokens[i + 1].ToLower().Equals("as"))
                {
                    string strOrigName = astrCmdTokens[i];
                    i = i + 2;
                    string strAsName = astrCmdTokens[i];
                    char? chrQuote = _findQuoteChar(strAsName);

                    if (chrQuote.HasValue)
                    {
                        while (!_findQuoteChar(strAsName, false, chrQuote).HasValue)
                        {
                            strAsName += " " + astrCmdTokens[++i];  // TODO: Kinda nasty side-effect of tokenizing too soon; all whitespace becomes a single space for now.
                        }
                        strAsName = strAsName.Trim(chrQuote.Value);
                        // asymmetric quoter.
                        if (chrQuote.Equals('['))
                        {
                            strAsName = strAsName.TrimEnd(']');
                        }
                    }
                    this.dictRawNamesToASNames.Add(strOrigName, strAsName);
                }
            }

            this.acolInSelect = qCols.ToArray();
        }

        // TODO: Right now, we're allowing any quote character to be doubled up as an escape value
        // for a single char of that "quoter". Is that what we want?
        private char? _findQuoteChar(string strIn, bool bStartNotFinish = true, char? chrSpecific = null)
        {
            char? chrRet = null;
            Queue<char> qstrQuotes = new Queue<char>(new[] { '`', '\'', '"' });

            qstrQuotes.Enqueue(bStartNotFinish ? '[' : ']');

            if (chrSpecific.HasValue)
            {
                chrSpecific = chrSpecific.Value.Equals('[') && !bStartNotFinish ? ']' : chrSpecific;    // TODO: Reconsider. This is in case we were returned '[' on the first call, but now really want ']', so we don't have to sniff in calling code.
                qstrQuotes = new Queue<char>(new[] { chrSpecific.Value });
            }

            foreach (char chr in qstrQuotes)
            {
                if (
                    (bStartNotFinish && strIn.StartsWith(Char.ToString(chr)) && !strIn.StartsWith(new string(chr, 2)))
                    ||
                    (!bStartNotFinish && strIn.EndsWith(Char.ToString(chr)) && !strIn.EndsWith(new string(chr, 2)))
                )
                {
                    chrRet = chr;
                    break;
                }
            }

            return chrRet;
        }
    }

}
