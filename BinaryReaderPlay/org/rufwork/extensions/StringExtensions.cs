using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace org.rufwork.extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Will call ContainsOutsideOfQuotes with ignore case, quotes are '
        ///
        /// Note: I'm not handling stringComparison yet.
        /// </summary>
        /// <param name="strToFind">String that we want to find outside of quotes in the "calling" or parent string.</param>
        /// <returns></returns>
        public static bool ContainsOutsideOfQuotes(this string str, string strToFind)
        {
            return str.ContainsOutsideOfQuotes(strToFind, StringComparison.CurrentCultureIgnoreCase, '\'');
        }

        /// <summary>
        /// Will call ContainsOutsideOfQuotes with ignore case.
        ///
        /// Note: I'm not handling stringComparison yet.
        /// </summary>
        /// <param name="strToFind">String that we want to find outside of quotes in the "calling" or parent string.</param>
        /// <param name="astrSplittingTokens">The tokens that can be used to declare the start and stop of an
        /// escaped string.</param>
        /// <returns>True if it's there, false if it isn't.</returns>
        public static bool ContainsOutsideOfQuotes(this string str, string strToFind, params char[] astrSplittingTokens)
        {
            return str.ContainsOutsideOfQuotes(strToFind, StringComparison.CurrentCultureIgnoreCase, astrSplittingTokens);
        }

        /// <summary>
        /// Find if a string's in another, but ignore anything within "Quotes".
        /// So if you're looking for "test" within "This is 'a test' isn''t it?", it'd return false, because
        /// "test" is within ' and '.
        ///
        /// Note: I'm not handling stringComparison yet.
        /// </summary>
        /// <param name="strToFind">String that we want to find outside of quotes in the "calling" or parent string.</param>
        /// <param name="stringComparison">Currently ignored.</param>
        /// <param name="astrSplittingTokens">The tokens that can be used to declare the start and stop of an
        /// escaped string.</param>
        /// <returns>True if it's there, false if it isn't.</returns>
        public static bool ContainsOutsideOfQuotes(this string str, string strToFind,
            StringComparison stringComparison,
            params char[] astrSplittingTokens)
        {
            bool foundIt = false;

            for (int i = 0; i < str.Length; i++)
            {
                while (i < str.Length && !astrSplittingTokens.Contains(str[i]) && !str[i].Equals(strToFind[0]))
                    i++;

                if (i < str.Length)
                {
                    if (astrSplittingTokens.Contains(str[i]))
                    {
                        char splitterFound = str[i];
                        i++;
                        while (i < str.Length)
                        {
                            if (str[i].Equals(splitterFound))
                                if (i + 1 < str.Length)
                                    if (str[i + 1].Equals(splitterFound))
                                        i = i + 2;
                                    else
                                        break;
                                else
                                    break;  // we're at the end of `str`; it'll kick out in the initial while in the next pass.
                            else
                                i++;
                        }
                    }
                    else
                    {
                        // else this should be equal to the first char in the search string.
                        int foundStart = i;
                        while (i < str.Length && i - foundStart < strToFind.Length && str[i].Equals(strToFind[i - foundStart]))
                            i++;
                        if (i - foundStart == strToFind.Length)
                        {
                            foundIt = true;
                            break;
                        }
                    }
                }
            }

            return foundIt;
        }

        // Another cheesy regular expression end run.  Don't overcomplicate jive.
        // This should split up strings with multiple commands into, well, multiple commands.
        // Tokens within backticks are ignored.
        // TODO: Consider making this smarter/not depend on the s/'/` kludge.
        public static Queue<String> SplitSeeingQuotes(this string strToSplit, string strSplittingToken, bool bIncludeToken, bool bTrimResults = true)
        {
            Queue<string> qReturn = new Queue<string>();
            StringBuilder stringBuilder = new StringBuilder();

            // TODO: A smarter way to ensure you're comparing apples to apples
            // in the first byte knockout comparison.
            string STRTOSPLIT = strToSplit.ToUpper();
            string STRSPLITTINGTOKEN = strSplittingToken.ToUpper();

            bool inSingleQuotes = false;
            bool inBackTicks = false;

            for (int i = 0; i < strToSplit.Length; i++)
            {
                // TOOD: Reconsider efficiency of these checks.
                if (!inSingleQuotes && '`' == strToSplit[i])
                {
                    inBackTicks = !inBackTicks;
                    stringBuilder.Append(strToSplit[i]);
                }
                else if ('\'' == strToSplit[i])
                {
                    inSingleQuotes = !inSingleQuotes;
                    stringBuilder.Append(strToSplit[i]);
                }
                else if (!inSingleQuotes && !inBackTicks
                    && STRSPLITTINGTOKEN[0] == STRTOSPLIT[i]
                    && strToSplit.Length >= i + strSplittingToken.Length
                    && strSplittingToken.Equals(strToSplit.Substring(i, strSplittingToken.Length), StringComparison.CurrentCultureIgnoreCase))
                {
                    if (bIncludeToken)
                    {
                        stringBuilder.Append(strSplittingToken);
                    }
                    if (stringBuilder.Length > 0 && (!bTrimResults || stringBuilder.ToString().Trim().Length > 0))
                    {
                        qReturn.Enqueue(stringBuilder.ToString());
                        stringBuilder = new StringBuilder();
                    }
                    i = i + (strSplittingToken.Length - 1); // -1 for the char we've already got in strToSplit[i]
                }
                else
                {
                    stringBuilder.Append(strToSplit[i]);
                }
            }

            if (stringBuilder.Length > 0 && (!bTrimResults || stringBuilder.ToString().Trim().Length > 0))
            {
                qReturn.Enqueue(stringBuilder.ToString());
            }

            return qReturn;
        }

        // Only replace double single quotes inside of single quotes.
        public static string BacktickQuotes(this string strToClean)
        {
            bool inQuotes = false;
            string strOut = string.Empty;
            for (int i = 0; i < strToClean.Length - 1; i++)
            {
                if (strToClean[i].Equals('\'') && strToClean[i + 1].Equals('\''))
                {
                    strOut += inQuotes ? "`" : "''";
                    i++;
                }
                else if (strToClean[i].Equals('\''))
                {
                    strOut += '\'';
                    inQuotes = !inQuotes;
                }
                else
                    strOut += strToClean[i];
            }
            strOut += strToClean[strToClean.Length - 1];
            //Console.WriteLine(strOut);
            return strOut;
        }

        // Yes, I gave up on RegExp and used a char array.  Sue me.
        // Honestly, this is much more straightforward.  It's like a regexp
        // as an exploded view.
        public static string[] SqlToTokens(this string strToToke)
        {
            char[] achrSql = strToToke.ToCharArray();
            Queue<string> qString = new Queue<string>();
            string strTemp = "";

            for (int i = 0; i < achrSql.Length; i++)
            {
                switch (achrSql[i])
                {
                    case ' ':
                    case '\r':
                    case '\n':
                    case '\t':
                        if (!string.IsNullOrWhiteSpace(strTemp))
                        {
                            qString.Enqueue(strTemp);
                        }
                        strTemp = "";
                        while (string.IsNullOrWhiteSpace(achrSql[i].ToString()) && i < achrSql.Length)
                        {
                            i++;
                        }
                        if (i < achrSql.Length)
                        {
                            i--;    // pick back up with next non-space character.
                        }
                        break;

                    case '\'':
                        i++;
                        while ('\'' != achrSql[i])
                        {
                            strTemp += achrSql[i++];
                        }
                        qString.Enqueue("'" + strTemp + "'");
                        strTemp = "";
                        break;

                    case '(':
                    case ')':
                        break;

                    case ',':
                        if (string.Empty != strTemp.Trim())
                        {
                            qString.Enqueue(strTemp);
                        }
                        strTemp = "";
                        break;

                    // TODO: Handle functions more cleanly.  Maybe translate to easily parsed, paren-less strings?
                    // This is EMBARRASSINGLY hacky.
                    case 'N':
                        if (i + 4 < achrSql.Length)
                        {
                            if (achrSql[i + 1].Equals('O')
                                && achrSql[i + 2].Equals('W')
                                && achrSql[i + 3].Equals('(')
                                && achrSql[i + 4].Equals(')')
                            )
                            {
                                qString.Enqueue("NOW()");
                                i = i + 4;
                                strTemp = "";
                            }
                            else
                            {
                                goto default;
                            }
                        }
                        break;

                    default:
                        strTemp += achrSql[i];
                        break;
                }
            }
            if (string.Empty != strTemp.Trim())
            {
                qString.Enqueue(strTemp);
            }
            return qString.ToArray();
        }

        public static int CountCharInString(this string strToSearch, string strToFind)
        {
            return strToSearch.Length - (strToSearch.Replace(strToFind, "").Length / strToFind.Length);
        }

        public static string RemoveNewlines(this string strIn, string strReplacement)
        {
            return Regex.Replace(strIn, @"\r\n?|\n", strReplacement);
        }

        public static string ScrubValue(this string strToScrub)
        {
            string strReturn = strToScrub;

            strReturn = strReturn.Replace("'", "''");

            return strReturn;
        }

        private static bool _NotEmptyString(String s)
        {
            return !s.Equals("");
        }

        public static string[] StringToNonWhitespaceTokens(this string strToToke)
        {
            string[] astrAllTokens = strToToke.Split();
            string[] astrCmdTokens =
                Array.FindAll(astrAllTokens, _NotEmptyString);
            return astrCmdTokens;
        }

        // What's the advantage over .Split()?  Specific whitespaces?
        public static string[] StringToNonWhitespaceTokens2(this string strToToke)
        {
            return Regex.Split(strToToke, @"[\(\)\s,]+").Where(s => s != String.Empty).ToArray<string>(); // TODO: Better way of doing this.  Can I add to regex intelligently?
        }
    }
}