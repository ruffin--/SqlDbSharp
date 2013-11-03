//Copyright (C) 2012-3  Ruffin Bailey
//
//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this single source file (the “Software”), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:
//
//The above copyright notice and this permission notice shall be included in
//all copies or substantial portions of the Software.
//
//THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.
using System;
using System.IO;
using org.rufwork;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Data;

namespace org.rufwork
{
    public class Utils
    {

        // TODO: Rename static methods with capitals
        
        private static object _LogLock = 0;     // needs to be at the containing object's level
        // so, in this case, static object global
        
        public static readonly string cstrHomeDir = System.Environment.GetFolderPath (System.Environment.SpecialFolder.Personal);
        
        public static void logErr (string strErrDesc, string strLoc)
        {
            Console.WriteLine ("ERROR: " + strLoc + "\n" + strErrDesc);
            Utils.logStuff ("ERROR: " + strLoc + "\n" + strErrDesc);
        }
        
        // Convenience function to log jive.
        public static void logStuff (string strToLog)
        {
            // obviously the folder must exist
            string strLogFile = System.IO.Path.Combine (Utils.cstrHomeDir, "appLog.log");
            string strDateFormat = "MM/dd/yyyy hh:mm:ss.fff tt";
            
            try
            {
                // When you have multiple AJAX (because, duh, asynchronous)
                // calls going at once which, each of which might be calling
                // parts of your .NET website that log stuff, you're likely to
                // eventually run into concurrent logStuff calls.  And since
                // one "logStuff" might have your log file locked when the
                // second comes along and also wants to log some stuff, you're
                // going to want to lock the process so that you don't throw
                // exceptions.  Now, the second logStuff call will wait
                // patiently until the lock on _LogLock is released.
                
                // More information here:
                // http://stackoverflow.com/questions/7419172/streamwriter-lock-not-working
                // http://msdn.microsoft.com/en-us/library/c5kehkcz(v=vs.71).aspx
                lock (_LogLock) {
                    if (!File.Exists (strLogFile)) {
                        // Create a file to write to.
                        using (StreamWriter sw = File.CreateText(strLogFile)) {
                            sw.WriteLine ("Our application's log file.");
                            sw.WriteLine ("Created " + DateTime.Now.ToString (strDateFormat));
                            sw.Close ();
                        }
                    }
                    
                    // This text is always added, making the file longer over time
                    // if it is not deleted or cleaned up.
                    using (StreamWriter sw = File.AppendText(strLogFile)) {
                        sw.WriteLine (DateTime.Now.ToString (strDateFormat) + ":");
                        sw.WriteLine ("\t" + strToLog.Replace ("\n", "\n\t"));
                        sw.Close ();
                    }
                }
            } catch (Exception) {
                // probably b/c log location doesn't exist or isn't accessible.
                System.Diagnostics.Debugger.Break ();    
                
                // insure that's the problem.
                // In production, if you forget to remove this exception handler
                // or to move log location to something valid on the server/box, 
                // it'll quietly fail.
            }
            
        }
        
        
        public static string scrubValue(string strToScrub)    {
            string strReturn = strToScrub;
            
            strReturn.Replace("'", "''");
            
            return strReturn;
        }
        
        // put debugging here so it's easier to blast or reroute later.
        public static void write2Console (string strToWrite)
        {
            Console.WriteLine (strToWrite);
        }
        
        
        public static string[] splitExtension (string strFileName)
        {
            string[] astrReturn = new string[2];
            
            int intLastDot = strFileName.LastIndexOf (".");
            if (intLastDot > 0) {    // > 0 and not >= 0 b/c .vimrc is a file name.
                astrReturn [0] = strFileName.Substring (0, intLastDot);
                astrReturn [1] = strFileName.Substring (intLastDot, strFileName.Length - intLastDot);
            } else {
                astrReturn [0] = strFileName;
                astrReturn [1] = "";
            }
            return astrReturn;
        }

        public static string printByte(byte bytIn)
        {
            string strReturn = bytIn.ToString("X2");
            if (bytIn >= 32 && bytIn <= 126)
            {
                strReturn = " " + (char)bytIn;
            }
            return strReturn;
        }

        public static int string2int (string strIn)
        {
            int intReturn = -13371337;
            
            if ("13377331".Equals (strIn)) {
                throw new Exception ("You have found some poor programming");
            } else {                
                if (!int.TryParse (strIn, out intReturn))
                    intReturn = 13377331;
            }            
            return intReturn;
        }

        // Assuming positive ints here.
        // Actually it works either way.
        // int is an int32, so four bytes is what we want.
        static public int byteArrayToInt(byte[] abytIn)
        {
            int intReturn = 0;

            if (abytIn.Length > 4) 
            {
                throw new Exception("Illegal byte array for Int: " + abytIn.Length);
            }


            for (int i=0; i<abytIn.Length; i++) {
                intReturn += (int)Math.Pow(256, abytIn.Length - (i + 1)) * abytIn[i];   // should probably check to make sure this doesn't overshoot an int.
                // Note to self -- C# has no ^ operator for exponents.  It is, instead, xor.
                // http://blogs.msdn.com/b/csharpfaq/archive/2004/03/07/why-doesn-t-c-have-a-power-operator.aspx
            }
            
            return intReturn;
        }
        
        // Assuming positive values here.
        // Actually it works either way.
        // int is an int64, so eight bytes is what we want.
        static public long byteArrayToLong (byte[] abytIn)
        {
            long lngReturn = 0;

            if (abytIn.Length > 8) 
            {
                throw new Exception("Illegal byte array for Long: " + abytIn.Length);
            }
            
            for (int i=0; i<abytIn.Length; i++) {
                lngReturn += (long)Math.Pow(256, abytIn.Length - (i + 1)) * abytIn[i];   // should probably check to make sure this doesn't overshoot an int.
                // Note to self -- C# has no ^ operator for exponents.  It is, instead, xor.
                // http://blogs.msdn.com/b/csharpfaq/archive/2004/03/07/why-doesn-t-c-have-a-power-operator.aspx
            }
            
            return lngReturn;
        }

        /// <summary>
        /// Returns array with most significant byte first.
        /// If a length is provided, it will pad to the left
        /// to fill to that length.
        /// </summary>
        /// <param name="intIn"></param>
        /// <param name="intArrayLength"></param>
        /// <returns></returns>
        static public byte[] intToByteArray(int intIn, int intArrayLength = 0)
        {
            byte[] abytInteger;

            // Zero throws off the Peek and Dequeue routine, below,
            // so we'll eliminate that case first.
            if (0 == intIn)
            {
                abytInteger = new byte[0x00];
            }
            else
            {
                abytInteger = BitConverter.GetBytes(intIn);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(abytInteger);

                if (abytInteger.Length != intArrayLength)
                {
                    // Get rid of bytes that are leading zeroes.
                    Queue<byte> qTemp = new Queue<byte>(abytInteger);
                    while (qTemp.Peek() == 0)
                    {
                        qTemp.Dequeue();
                    }
                    abytInteger = qTemp.ToArray();  // back to an array and return.
                }
            }

            if (0 < intArrayLength && abytInteger.Length != intArrayLength)
            {
                if (abytInteger.Length > intArrayLength)
                {
                    throw new Exception("Request length is not long enough to contain value.");
                }
                byte[] abytPadded = new byte[intArrayLength];
                Buffer.BlockCopy(abytInteger, 0, abytPadded, abytPadded.Length - abytInteger.Length, abytInteger.Length);
                abytInteger = abytPadded;
            }

            return abytInteger;
        }

        // Returns array with most significant byte first.
        // TODO: Close enough to int to consider merging.
        static public byte[] longToByteArray(long lngIn)
        {
            byte[] abytInteger;

            // Zero throws off the Peek and Dequeue routine, below,
            // so we'll eliminate that case first.
            if (0 == lngIn)
            {
                abytInteger = new byte[0x00];
            }
            else
            {
                abytInteger = BitConverter.GetBytes(lngIn);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(abytInteger);

                // Get rid of bytes that are leading zeroes.
                Queue<byte> qTemp = new Queue<byte>(abytInteger);
                while (qTemp.Peek() == 0)
                {
                    qTemp.Dequeue();
                }
                abytInteger = qTemp.ToArray();  // back to an array and return.
            }

            return abytInteger;
        }

        // There are cleverer ways to do this.  Taken from question
        // detailing some of those cleverer ways:
        // http://stackoverflow.com/questions/43289/comparing-two-byte-arrays-in-net
        // I don't like the immediate returns, but I'm betting it
        // helps performance.  I don't have any performant uses,
        // but I'll leave it for now.
        static public bool AreByteArraysEqual(byte[] a1, byte[] a2) 
        {
            if(a1.Length!=a2.Length)
                return false;
            
            for(int i=0; i<a1.Length; i++)
                if(a1[i]!=a2[i])
                    return false;
            
            return true;
        }



        #region IOUtils
        // TODO: Should probably move I/O utilities to a more specialized file.
        // This from http://stackoverflow.com/a/221941/1028230
        public static byte[] ReadFully(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
        // From Skeet:
        // Likewise you can put a check at the end, and if the length of the stream is the
        // same size as the buffer (returned by MemoryStream.GetBuffer) then you can just
        // return the buffer. So the above code isn't quite optimised, but will at least
        // be correct.
        // I'm really not worrying about the stream not having everything ready at the time
        // of reading.  That should be safe in our case -- we're not reading from a network
        // pipe or whatever.
        public static byte[] ReadToLength(Stream input, int intLengthToRead)
        {
            return ReadToLength(input, intLengthToRead, 0);
        }
        public static byte[] ReadToLength(Stream input, int intLengthToRead, int intZeroBasedIndexToStart)
        {
            byte[] abytReturn = null;

            // TODO: Potentially remove try-catch now
            try
            {
                byte[] buffer = new byte[intLengthToRead];
                input.Seek(intZeroBasedIndexToStart, SeekOrigin.Begin);
                using (MemoryStream ms = new MemoryStream())
                {
                    int read;
                    if ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, read);
                    }
                    abytReturn = ms.ToArray();
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Unsuccessful ReadToLength call. " + ex.ToString());
                abytReturn = null;
            }

            return abytReturn;
        }
        #endregion IOUtils

        private static bool _NotEmptyString(String s)
        {
            return !s.Equals("");
        }
        public static string[] stringToNonWhitespaceTokens(string strToToke)
        {
            string[] astrAllTokens = strToToke.Split();
            string[] astrCmdTokens =
                Array.FindAll(astrAllTokens, _NotEmptyString);
            return astrCmdTokens;
        }

        // What's the advantage over .Split()?  Specific whitespaces?
        public static string[] stringToNonWhitespaceTokens2(string strToToke)
        {
            return Regex.Split(strToToke, @"[\(\)\s,]+").Where(s => s != String.Empty).ToArray<string>(); // TODO: Better way of doing this.  Can I add to regex intelligently?
        }

        // Yes, I gave up on RegExp and used a char array.  Sue me.
        // Honeslty, this is much more straightforward.  It's like a regexp
        // as an exploded view.
        public static string[] SqlToTokens(string strToToke)
        {
            char[] achrSql = strToToke.ToCharArray();
            Queue<string> qString = new Queue<string>();
            string strTemp = "";

            for (int i = 0; i < achrSql.Length; i++)
            {
                switch (achrSql[i])
                {
                    case ' ':
                        if (string.Empty != strTemp)
                        {
                            qString.Enqueue(strTemp);
                        }
                        while (' ' == achrSql[i] && i < achrSql.Length)
                        {
                            i++;
                        }
                        strTemp = "";
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
                    case ',':
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

        public static int countCharInString(string strToSearch, string strToFind)
        {
            return strToSearch.Length - (strToSearch.Replace(strToFind, "").Length / strToFind.Length);
        }

        public static string removeNewlines(string strIn, string strReplacement)  {
            return Regex.Replace(strIn, @"\r\n?|\n", strReplacement);
        }

        public Utils ()
        {
        }
    }
}