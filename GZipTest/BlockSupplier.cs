using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GZipTest
{
    // Класс, предназначенный для выдачи порций байтов заданного размера
    // из переданного ему потока.
    internal abstract class BlockSupplier
    {
        protected Stream SourceStream { get; }

        protected int PartNumber { get; set; }

        protected BlockSupplier(Stream sourceStream)
        {
            SourceStream = sourceStream;
        }

        // Считывает из SourceStream блок данных
        // Никогда не возвращает null, в случае EOF 
        // поле DataBlock.Size будет равно 0
        public abstract DataBlock Next();
    }

    // Класс, который предназначен для считывания данных для упаковки.
    // Считывает данные безотносительно их содержания.
    internal class NonCompressedBlockSupplier : BlockSupplier
    {
        private int _blockSize;

        public NonCompressedBlockSupplier(Stream sourceStream, int blockSize) : base(sourceStream)
        {
            _blockSize = blockSize;    
        }

        public override DataBlock Next()
        {
            lock (SourceStream)
            {
                var dataBlock = new DataBlock(PartNumber++, new byte[_blockSize]);

                int bytesRead, offset = 0;

                do
                {
                    bytesRead = SourceStream.Read(dataBlock.Data, offset, dataBlock.Size - offset);
                    offset += bytesRead;
                }
                while (offset != dataBlock.Size && bytesRead != 0);

                dataBlock.Resize(offset);

                return dataBlock;
            }
        }
    }

    // Класс, который предназначен для считывания данных для распаковки.
    // Ожидает, что в поле MTIME заголовка будет записан размер блока
    internal class GZipCompressedBlockSupplier : BlockSupplier
    {
        public GZipCompressedBlockSupplier(Stream sourceStream) : base(sourceStream)
        {
        }

        public override DataBlock Next()
        {
            lock (SourceStream)
            {
                var buf = new byte[10];
                var bytesRead = SourceStream.Read(buf, 0, buf.Length);
                if (bytesRead == 0) return new DataBlock(PartNumber++, new byte[0]);
                if (buf[0] != 0x1f || buf[1] != 0x8b || buf[2] != 0x08)
                    throw new InvalidDataException("Archive is not valid or it was not created by this program.");

                var blockSize = BitConverter.ToInt32(buf, 4);
                buf = new byte[blockSize];

                SourceStream.Position -= 10;
                var offset = 0;

                // Stream.Read не гарантирует заполнение буфера при вызове
                // Цикл нужен, чтобы гарантировать считывание блока
                do
                {
                    bytesRead = SourceStream.Read(buf, offset, buf.Length - offset);
                    offset += bytesRead;
                }
                while (offset != blockSize && bytesRead != 0);

                return new DataBlock(PartNumber++, buf);
            }
        }
    }
}
