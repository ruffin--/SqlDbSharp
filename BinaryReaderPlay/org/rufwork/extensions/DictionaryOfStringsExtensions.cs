using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace org.rufwork.extensions
{
    public static class DictionaryOfStringsExtensions
    {
        // Better might be using Skeet's BiLookup: http://stackoverflow.com/a/255638/1028230
        public static string XGetFirstKeyByValue<T>(this Dictionary<string, string> dictionary, string strValue)
        {
            bool bFound = false;    // going to be overly explicit here.
            string strFirstKey = string.Empty;

            foreach (KeyValuePair<string, string> kvp in dictionary)
            {
                if (strValue.Equals(kvp.Value))
                {
                    strFirstKey = kvp.Key;
                    bFound = true;
                    break;
                }
            }

            if (!bFound)
            {
                throw new Exception("Value not found in dictionary: " + strValue);
            }

            return strFirstKey;
        }
    }
}
