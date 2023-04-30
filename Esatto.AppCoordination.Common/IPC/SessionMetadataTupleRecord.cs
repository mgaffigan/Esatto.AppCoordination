using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.AppCoordination.IPC
{
    [ComVisible(true)]
    public struct SessionMetadataTupleRecord
    {
        [MarshalAs(UnmanagedType.BStr)]
        public string Key;

        [MarshalAs(UnmanagedType.BStr)]
        public string Value;

        public SessionMetadataTupleRecord(string key, string value)
        {
            this.Key = key;
            this.Value = value;
        }

        #region Equality

        public override string ToString() => $"{Key}: '{Value}'";

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public static bool operator ==(SessionMetadataTupleRecord op1, SessionMetadataTupleRecord op2)
        {
            return op1.Equals(op2);
        }

        public static bool operator !=(SessionMetadataTupleRecord op1, SessionMetadataTupleRecord op2)
        {
            return !op1.Equals(op2);
        }

        #endregion
    }
}
