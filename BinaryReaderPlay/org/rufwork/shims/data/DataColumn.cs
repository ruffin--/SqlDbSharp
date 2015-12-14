using System;

namespace org.rufwork.shims.data
{
    public class DataColumn
    {
        public int MaxLength = -1;  // not used. for duck typing only.
        private int _requestedOrdinal = Int32.MinValue;

        private System.Type _dataType = null;
        public System.Type DataType
        {
            get
            {
                return _dataType;
            }
            set
            {
                if (null == value)
                {
                    throw new Exception("A DataColumn's DataType cannot be null.");
                }
                _dataType = value;
            }
        }
        public string ColumnName = string.Empty;

        public DataColumn(string strName)
        {
            this.ColumnName = strName;
        }

        public void SetOrdinal(int intRequestedOrdinal)
        {
            _requestedOrdinal = intRequestedOrdinal;
        }

        public int GetRequestedOrdinal()
        {
            return _requestedOrdinal;
        }
    }
}

