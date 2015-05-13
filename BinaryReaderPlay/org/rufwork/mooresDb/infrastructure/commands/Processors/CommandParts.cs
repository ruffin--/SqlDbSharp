using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using org.rufwork.mooresDb.infrastructure.contexts;
using org.rufwork.mooresDb.infrastructure.tableParts;
using org.rufwork.extensions;
using org.rufwork.mooresDb.exceptions;

namespace org.rufwork.mooresDb.infrastructure.commands.Processors
{
    // convenience class to hold different parts of the SELECT
    // statement's text.
    public class CommandParts
    {
        public string strOriginal;

        public string strSelect;
        public string strUpdate;
        public string strFrom;
        public string strWhere;
        public string strOrderBy;
        public string strInnerJoinKludge;
        public Queue<string> qInnerJoinFields = new Queue<string>();

        public string strTableName; // TODO: Ob need to go to a collection of some sort

        public Column[] acolInSelect;
        public Queue<string> qstrAllColumnNames = new Queue<string>();
        public List<string> lstrMainTableJoinONLYFields = new List<string>();   // Sir Not Well Named in this Film.
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

            intIndexOf = strSql.IndexOf("ORDER BY", StringComparison.CurrentCultureIgnoreCase);
            if (-1 < intIndexOf)
            {
                this.strOrderBy = strSql.Substring(intIndexOf, intTail - intIndexOf);
                this.strOrderBy = System.Text.RegularExpressions.Regex.Replace(this.strOrderBy, @"[\s\n]+", " ");   // flatten whitespace to a single space.
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
                        string strMainTableName = this.strFrom.Substring(4).Trim();
                        string[] innerKludgeTokenized = this.strInnerJoinKludge.Split();
                        foreach (string toke in innerKludgeTokenized)
                        {
                            if (toke.ToUpper().StartsWith(strMainTableName.ToUpper()))
                            {
                                this.lstrMainTableJoinONLYFields.Add(toke.ReplaceCaseInsensitiveFind(strMainTableName + ".", ""));
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

        private void _getColumnsToReturn()
        {
            Queue<Column> qCols = new Queue<Column>();
            List<string> lstrCmdTokens = this.strSelect.StringToNonWhitespaceTokens2().Skip(1).ToList();   // Skip 1 to ignore SELECT.

            if (!string.IsNullOrEmpty(this.strInnerJoinKludge))
            {
                // TODO: Clean this kludge to get in INNER JOIN fields into datatable
                // while selecting, but to remove these once we're done before
                // returning the DataTable.
                // NOTE: I'm not taking into account fuzzily matching names, in part
                // because I'm planning to remove that painful feature.
                if (lstrCmdTokens.Contains("*"))
                {
                    // If we're selecting everything from the main table,
                    // no additional columns are needed.
                    this.lstrMainTableJoinONLYFields = new List<string>();
                }
                else
                {
                    foreach (string strMainTableCol in lstrCmdTokens.Where(s => !s.Contains(".") && !s.Contains("*")))
                    {
                        if (this.lstrMainTableJoinONLYFields.Contains(strMainTableCol))
                        {
                            this.lstrMainTableJoinONLYFields.Remove(strMainTableCol);
                        }
                    }
                }

                foreach (string strJoinColName in this.lstrMainTableJoinONLYFields)
                {
                    lstrCmdTokens.Add(strJoinColName);
                }
            }   // end kludge for grafting main table join fields not explicitly in SELECT list.
            string[] astrCmdTokens = lstrCmdTokens.ToArray();

            for (int i = 0; i < astrCmdTokens.Length; i++)
            {
                // TODO: Handle * check with regexp or something. .Equals (in place of .Contains) is legit here if trimmed in StringToNonWhitespaceTokens2, yes?
                // TODO: Do this more intelligently, perhaps looking up likely tables for non-ambiguous column names, then treating every column as an explicit member
                //      of a certain table.
                if (!astrCmdTokens[i].EndsWith("*"))
                {
                    qstrAllColumnNames.Enqueue(astrCmdTokens[i]);

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
