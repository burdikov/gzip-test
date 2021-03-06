﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Класс, представляющий собой блок данных. -- К.О.
namespace GZipTest
{
    class DataBlock
    {
        public int ID { get; }

        private byte[] _data;
        public byte[] Data { get => _data; set { _data = value; } }

        public int Size
        {
            get
            {
                return Data?.Length ?? 0;
            }
        }

        public DataBlock(int id, byte[] data)
        {
            ID = id;
            Data = data;
        }

        public void Resize(int newSize)
        {
            Array.Resize(ref _data, newSize);
        }

        public override string ToString() => $"ID: {ID}";
        
    }
}
