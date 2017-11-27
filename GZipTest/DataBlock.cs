using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GZipTest
{
    class DataBlock
    {
        public int ID { get; }
        public byte[] Data { get; }
        public int Size
        {
            get
            {
                return Data?.Length ?? 0;
            }
        }

        public DataBlock(byte[] data, int id)
        {
            ID = id;
            Data = data;
        }
    }
}
