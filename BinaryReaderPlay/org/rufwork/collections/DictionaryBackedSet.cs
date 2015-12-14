using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using org.rufwork.shims;

namespace org.rufwork.collections
{
    public class DictionaryBackedSet<T> : IEnumerable<T>
    {
        protected readonly Dictionary<object, T> dict = new Dictionary<object, T>();

        public IEnumerator<T> GetEnumerator()
        {
            return dict.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Add(T item)
        {
            if (Contains(item))
            {
                return false;
            }
            // Hacky for testing now. Extended in DataColumnSet
            string strRandomKey = Guid.NewGuid().ToString();
            PCLConsole.WriteLine("Random key: " + strRandomKey);
            dict.Add(strRandomKey, item);
            return true;
        }

        public bool Contains(T item)
        {
            return dict.ContainsValue(item);
        }

        public int Count
        {
            get
            {
                return dict.Count;
            }
        }

        public virtual T this[int intIndex]
        {
            get
            {
                return dict.ElementAt(intIndex).Value;
            }
        }
    }
}

