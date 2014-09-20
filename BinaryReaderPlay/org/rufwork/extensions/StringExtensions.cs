// =========================== LICENSE ===============================
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
// ======================== EO LICENSE ===============================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace org.rufwork.extensions
{
    public static class StringExtensions
    {
        public static string DraconianWrap(this string str, int lineLength, string lineEnding = "\n")
        {
            StringBuilder stringBuilder = new StringBuilder();
            int stringPos = 0;
            while (stringPos < str.Length)
            {
                int substringLength = stringPos + lineLength > str.Length ? str.Length - stringPos : lineLength;
                string substring = str.Substring(stringPos, substringLength);
                stringBuilder.Append(substring + lineEnding);
                stringPos += substringLength;
            }
            return stringBuilder.ToString();
        }

        public static Stream ToStream(this string str)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(str);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        // Note that this doesn't work with Hebrew characters with vowels,
        // apparently (though you could argue it kind of does, iiuc)
        // See stackoverflow.com/questions/15029238
        public static string ReverseString(this string str)
        {
            if (null == str)
                return null;

            char[] aReverseMe = str.ToCharArray();
            Array.Reverse(aReverseMe);
            return new string(aReverseMe);
        }

        public static int LengthUTF8(this string str)
        {
            return Encoding.UTF8.GetByteCount(str);
        }

        // Using `ToByte` just to illustrate cleanly that we're ANDing on
        // the leading bit.
        // Note that C# 6.0 might allow "real" binary representations:
        // http://roslyn.codeplex.com/wikipage?title=Language%20Feature%20Status
        public static string CutToUTF8Length(this string str, int byteLength)
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(str);
            string returnValue = string.Empty;

            if (byteArray.Length > byteLength)
            {
                int bytePointer = byteLength;

                // Check high bit to see if we're [potentially] in the middle of a multi-byte char
                if (bytePointer >= 0 && (byteArray[bytePointer] & Convert.ToByte("10000000", 2)) > 0)
                {
                    while (bytePointer >= 0 && Convert.ToByte("11000000", 2) != (byteArray[bytePointer] & Convert.ToByte("11000000", 2)))
                    {
                        bytePointer--;
                    }
                }

                // See if we had 1s in the high bit all the way back. If so, we're toast. Return empty string.
                if (0 != bytePointer)
                {
                    byte[] cutValue = new byte[bytePointer];
                    Array.Copy(byteArray, cutValue, bytePointer);
                    returnValue = Encoding.UTF8.GetString(cutValue, 0, cutValue.Length);
                }
            }
            else
            {
                returnValue = str;
            }

            return returnValue;
        }

        public static string ReplaceCaseInsensitiveFind(this string str, string findMe, string newValue)
        {
            return Regex.Replace(str,
                Regex.Escape(findMe),
                Regex.Replace(newValue, "\\$[0-9]+", @"$$$0"),
                RegexOptions.IgnoreCase);
        }

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

        public static string ExtractBetweenFirstInstanceofDelimiters(this string str, string delimiterStartAfter, string delimiterEndBefore)
        {
            string strRun = string.Empty;

            if (-1 < str.IndexOf(delimiterStartAfter))
            {
                strRun = str.Substring(str.IndexOf(delimiterStartAfter) + delimiterStartAfter.Length);
                if (-1 < strRun.IndexOf(delimiterEndBefore))
                {
                    strRun = strRun.Substring(0, strRun.IndexOf(delimiterEndBefore));
                }
                else
                {
                    strRun = string.Empty;  // No luck, Ending not after Start; go back to nothing.
                }
            }

            return strRun;
        }

        public static bool ContainsWhitespace(this string str)
        {
            Regex regexp = new Regex(@"\s");
            return regexp.IsMatch(str);
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

        public static string OperateOnNonQuotedChunks(this string str, 
            Func<string, string> chunkProcessor,
            params char[] astrSplittingTokens)
        {
            string strReturn = string.Empty;
            string strUnquotedChunk = string.Empty;

            for (int i = 0; i < str.Length; i++)
            {
                while (i < str.Length && !astrSplittingTokens.Contains(str[i]))
                {
                    strUnquotedChunk += str[i]; // I guess we could use indexOf, but that'd make the `params` more difficult to handle
                    i++;
                }
                strReturn += chunkProcessor(strUnquotedChunk);      // TODO: StringBuilder
                strUnquotedChunk = string.Empty;

                // So we're either at the end of the string or we're in a quote.
                if (i < str.Length)
                {
                    char splitterFound = str[i];
                    i++;
                    while (i < str.Length)
                    {
                        if (str[i].Equals(splitterFound))
                            // If we have two of the same splitter char in a row, we're going to 
                            // treat it as an escape sequence.
                            // TODO: Could make that optional.
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
            }

            return strReturn;
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

        public static string PadLeftWithMax(this string str, int intLength)
        {
            str = str.Length > intLength ? str.Substring(0, intLength) : str;
            return str.PadLeft(intLength);
        }

        public static Queue<String> SplitSeeingSingleQuotesAndBackticks(this string strToSplit, string strSplittingToken, bool bIncludeToken, bool bTrimResults = true)
        {
            return strToSplit.SplitSeeingQuotes(strSplittingToken, bIncludeToken, bTrimResults, '\'', '`');
        }

        // Another cheesy regular expression end run.  Don't overcomplicate jive.
        // This should split up strings with multiple commands into, well, multiple commands.
        // Remember the respect tokens within backticks to support MySQL style backtick quotes in 
        // statements like CREATE TABLE `DbVersion`...
        public static Queue<String> SplitSeeingQuotes(this string strToSplit, string strSplittingToken, bool bIncludeToken, bool bTrimResults, params char[] validQuoteChars)
        {
            Queue<string> qReturn = new Queue<string>();
            StringBuilder stringBuilder = new StringBuilder();

            // TODO: A smarter way to ensure you're comparing apples to apples
            // in the first byte knockout comparison.
            string STRTOSPLIT = strToSplit.ToUpper();
            string STRSPLITTINGTOKEN = strSplittingToken.ToUpper();

            bool inQuotes = false;
            char chrCurrentSplitter = validQuoteChars[0];   // dummy value.

            for (int i = 0; i < strToSplit.Length; i++)
            {
                // TOOD: Reconsider efficiency of these checks.
                if (!inQuotes && validQuoteChars.Contains(strToSplit[i]))
                {
                    inQuotes = true;
                    chrCurrentSplitter = strToSplit[i];
                    stringBuilder.Append(strToSplit[i]);
                }
                else if (inQuotes && strToSplit[i].Equals(chrCurrentSplitter))
                {
                    inQuotes = false;
                    stringBuilder.Append(strToSplit[i]);
                }
                else if (!inQuotes
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

        public static bool IsNumeric(this string str)
        {
            double dblDummy;
            return double.TryParse(str, out dblDummy);
            //return str.All(c => char.IsDigit(c) || c == '.'); // <<< Advantage is no issue with overflows, which might be a problem with double.TryParse.  I'll ignore that for now (I could wrap for an overflow exception and then fallback to this).
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
            // TODO: Why not `!string.IsNullOrEmpty(s)`?  Guess we can't get a null from
            // the split in StringToNonWhitespaceTokens and this is faster?
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

        // TODO: Consider having a "max lines to return" governor to make sure we don't get memory crazy.
        public static string[] LinesAsArray(this string str, int intWrapLength = -1)
        {
            string[] astrRun = str.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            if (intWrapLength > 1)
            {
                Queue<string> qRun = new Queue<string>();
                foreach (string strLine in astrRun)
                {
                    foreach (string strWrappedLine in strLine.DraconianWrap(intWrapLength).LinesAsArray())
                    {
                        qRun.Enqueue(strWrappedLine);
                    }
                }
                astrRun = qRun.ToArray();
            }
            return astrRun;
        }

        #region CodeProject
        // Source: http://www.codeproject.com/Articles/11902/Convert-HTML-to-Plain-Text
        // License: http://www.codeproject.com/info/cpol10.aspx
        // Note: This method has been edited from the version made above.
        // TODO: Why in heaven's name does this use \r for newlines?
        //      CR:    Commodore 8-bit machines, Acorn BBC, ZX Spectrum, TRS-80, Apple II family, Mac OS up to version 9 and OS-9 (Wikipedia 20140904)

        // Changes from page:
        // http://www.codeproject.com/Messages/3663477/Improvement-to-your-great-piece-of-work.aspx
        //When using your code with the System.Web.HttpUtility.HtmlEncode function, I
        //found that the output of the function produced "&amp;amp;lt;=" for "<=". This
        //string of characters became "&amp;lt;=" when using the
        //System.Web.HttpUtility.HtmlDecode function. This of course would then produce
        //"=" when passed through the StripHTML function.

        // http://www.codeproject.com/Messages/3436774/Did-not-work-in-this-case.aspx
        //the source string has something like 
        //<a onmouseover='This is text that has less than and greater than characters <> ... ' >I want to extract text here only</a>
        //How to get the text that is between open/close <a> tags <a>...</a>?

        // http://www.codeproject.com/Messages/4883435/fix-for-greedy-matching.aspx
        //fix for greedy matching Pin
        //member	tspitz	15-Aug-14 6:18 

        //i had to fix three instances of .* to .*? which switched from greedy to lazy matching. otherwise
        //System.Text.RegularExpressions.Regex.Replace(result, @"(<script>).*(</script>)"
        //removed all text between the first and last script on the page.
 
        //This works:
        //System.Text.RegularExpressions.Regex.Replace(result, @"(<script>).*?(</script>)"
        public static string StripHTML(this string source)
        {
            try
            {
                string result;

                // Remove HTML Development formatting
                // Replace line breaks with space
                // because browsers inserts space
                result = source.Replace("\r", " ");
                // Replace line breaks with space
                // because browsers inserts space
                result = result.Replace("\n", " ");
                // Remove step-formatting
                result = result.Replace("\t", string.Empty);
                // Remove repeating spaces because browsers ignore them
                result = System.Text.RegularExpressions.Regex.Replace(result,
                                                                      @"( )+", " ");

                // Remove the header (prepare first by clearing attributes)
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"<( )*head([^>])*>", "<head>",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"(<( )*(/)( )*head( )*>)", "</head>",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         "(<head>).*(</head>)", string.Empty,
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // remove all scripts (prepare first by clearing attributes)
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"<( )*script([^>])*>", "<script>",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"(<( )*(/)( )*script( )*>)", "</script>",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                //result = System.Text.RegularExpressions.Regex.Replace(result,
                //         @"(<script>)([^(<script>\.</script>)])*(</script>)",
                //         string.Empty,
                //         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"(<script>).*(</script>)", string.Empty,
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // remove all styles (prepare first by clearing attributes)
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"<( )*style([^>])*>", "<style>",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"(<( )*(/)( )*style( )*>)", "</style>",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         "(<style>).*(</style>)", string.Empty,
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                //==============================================================
                // <li> tag custom behavior -R
                // TODO: <ol> vs. <ul>
                //==============================================================
                // TODO: This is an overly specific fix for <p>'s immediately after <li>.
                result = System.Text.RegularExpressions.Regex.Replace(result,
                    @"<li[ ]*[^>]*>(<p[^>]*>|[^a-z0-9A-Z])*", "* ",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                    @"</li>", "\r",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                //==============================================================

                // insert tabs in spaces of <td> tags
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"<( )*td([^>])*>", "\t",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // insert line breaks in places of <BR> and <LI>^H^H^H^H^H tags
                // LI below now. -R
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"<( )*br( )*>", "\r",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // insert line paragraphs (double line breaks) in place
                // if <P>, <DIV> and <TR> tags
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"<( )*div([^>])*>", "\r\r",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"<( )*tr([^>])*>", "\r\r",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"<( )*p([^>])*>", "\r\r",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // TODO: Blockquote.

                // Remove remaining tags like <a>, links, images,
                // comments etc - anything that's enclosed inside < >
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"<[^>]*>", string.Empty,
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // replace special characters:
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @" ", " ",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"&bull;", " * ",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"&lsaquo;", "<",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"&rsaquo;", ">",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"&trade;", "(tm)",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"&frasl;", "/",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"&lt;", "<",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"&gt;", ">",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"&copy;", "(c)",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"&reg;", "(r)",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                //================================================================================
                // More chars that I've added -R
                // TODO: Really need to call a funct each time instead of repeating so much crud.
                //================================================================================
                result = System.Text.RegularExpressions.Regex.Replace(result,
                    @"&#8217;", "'",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                    @"(&#8220;|&#8221;)", "\"",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                    @"&#8212;", "--",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                //================================================================================
                //================================================================================

                // Remove all others. More can be added, see
                // http://hotwired.lycos.com/webmonkey/reference/special_characters/
                // Not that the above url dates this stuff at all.
                // https://web.archive.org/web/20060112044405/http://hotwired.lycos.com/webmonkey/reference/special_characters/
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         @"&(.{2,6});", string.Empty,
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // for testing
                //System.Text.RegularExpressions.Regex.Replace(result,
                //       this.txtRegex.Text,string.Empty,
                //       System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                // make line breaking consistent
                result = result.Replace("\n", "\r");

                // Remove extra line breaks and tabs:
                // replace over 2 breaks with 2 and over 4 tabs with 4.
                // Prepare first to remove any whitespaces in between
                // the escaped characters and remove redundant tabs in between line breaks
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         "(\r)( )+(\r)", "\r\r",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         "(\t)( )+(\t)", "\t\t",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         "(\t)( )+(\r)", "\t\r",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         "(\r)( )+(\t)", "\r\t",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                // Remove redundant tabs
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         "(\r)(\t)+(\r)", "\r\r",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                // Remove multiple tabs following a line break with just one tab
                result = System.Text.RegularExpressions.Regex.Replace(result,
                         "(\r)(\t)+", "\r\t",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                // Initial replacement target string for line breaks
                string breaks = "\r\r\r";
                // Initial replacement target string for tabs
                string tabs = "\t\t\t\t\t";
                for (int index = 0; index < result.Length; index++)
                {
                    result = result.Replace(breaks, "\r\r");
                    result = result.Replace(tabs, "\t\t\t\t");
                    breaks = breaks + "\r";
                    tabs = tabs + "\t";
                }

                // That's it...
                return result.Trim();   // ... except for this trim I'm adding.
            }
            catch (Exception e)
            {
                throw e;
            }
        }
        #endregion CodeProject
    }
}
