using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using org.rufwork.mooresDb.infrastructure.contexts;
using org.rufwork.mooresDb.infrastructure.tableParts;
using org.rufwork.extensions;

namespace org.rufwork.mooresDb.infrastructure.commands.Processors
{
    // convenience class to hold different parts of the SELECT
    // statement's text.
    public class CommandParts
    {
        public string strSelect;
        public string strUpdate;
        public string strFrom;
        public string strWhere;
        public string strOrderBy;
        public string strInnerJoinKludge;

        public string strTableName; // TODO: Ob need to go to a collection of some sort

        public Column[] acolInSelect;
        public Dictionary<string, string> dictUpdateColVals = new Dictionary<string, string>();
        public Dictionary<string, string> dictFuzzyToColNameMappings = new Dictionary<string, string>();
        public COMMAND_TYPES commandType;

        private DatabaseContext _database;
        private TableContext _tableContext;

        public enum COMMAND_TYPES { SELECT, UPDATE, DELETE, INSERT };

        public CommandParts(DatabaseContext database, TableContext table, string strSql, COMMAND_TYPES commandType)
        {
            _database = database;
            _tableContext = table;

            this.commandType = commandType;
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

        public void _parseSelectStatement(string strSql)
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
                // TODO: Another reserved word that we don't really want a table to be named ("join").
                this.strInnerJoinKludge = "";
                if (this.strFrom.IndexOf(" join ", StringComparison.CurrentCultureIgnoreCase) > -1)
                {
                    int intInnerJoin = this.strFrom.IndexOf("inner join", StringComparison.CurrentCultureIgnoreCase);
                    if (intInnerJoin > -1)
                    {
                        this.strInnerJoinKludge = this.strFrom.Substring(intInnerJoin);
                        this.strFrom = this.strFrom.Substring(0, intInnerJoin);

                        // TODO: Check the WHERE clause to see if anything belongs to the JOIN.
                        //do that ^^^
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

        public void _getColumnsToReturn()
        {
            Queue<Column> qCols = new Queue<Column>();
            string[] astrCmdTokens = this.strSelect.StringToNonWhitespaceTokens2().Skip(1).ToArray();   // Skip 1 to ignore SELECT.

            // kludge check for all columns/asterisk selector.
            if (this.strSelect.Contains("*"))
            {
                qCols = new Queue<Column>(_tableContext.getColumns());
            }

            for (int i = 0; i < astrCmdTokens.Length; i++)
            {
                // TODO: Handle * check with regexp or something. .Equals (in place of .Contains) is legit here if trimmed in StringToNonWhitespaceTokens2, yes?
                if (!astrCmdTokens[i].Contains("*"))
                {
                    Column colTemp = _tableContext.getColumnByName(astrCmdTokens[i]);
                    if (null != colTemp)
                    {
                        qCols.Enqueue(colTemp);
                        if (!colTemp.strColName.Equals(astrCmdTokens[i], StringComparison.CurrentCultureIgnoreCase))
                        {
                            this.dictFuzzyToColNameMappings.Add(astrCmdTokens[i], colTemp.strColName);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Did not find: " + astrCmdTokens[i]);
                        // I guess that should throw an exception.  You asked for a col that doesn't seem to exist.
                        throw new Exception("SELECT Column does not exist: " + astrCmdTokens[i]);
                    }
                }
            }

            this.acolInSelect = qCols.ToArray();
        }
    }


}
