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
        public int BlockSize { get; }

        protected int PartNumber { get; set; }

        protected BlockSupplier(Stream sourceStream, int blockSize)
        {
            SourceStream = sourceStream;
            BlockSize = blockSize;
        }

        // Записывает ссылку на считанные данные в переданный аргумент
        // и возвращает число считанных байтов.
        // Гарантирует, что если не достигнут конец потока, будет
        // возвращен как минимум один байт.
        public abstract int Next(out byte[] buf);
    }

    // Класс, который предназначен для считывания данных для упаковки.
    // Считывает данные безотносительно их содержания.
    internal class NonCompressedBlockSupplier : BlockSupplier
    {
        private byte[] _buf;

        public NonCompressedBlockSupplier(Stream sourceStream, int blockSize) : base(sourceStream, blockSize)
        {
            _buf = new byte[BlockSize];
        }

        public override int Next(out byte[] buf)
        {
            lock (SourceStream)
            {
                var bytesRead = SourceStream.Read(_buf, 0, _buf.Length);

                buf = new byte[bytesRead];
                Array.Copy(_buf, buf, bytesRead);

                return bytesRead;
            }
        }
    }

    // Класс, который предназначен для считывания данных для распаковки.
    // Ищет в потоке заголовок gzip-архива (0x1f 0x8b 0x08) и возвращает
    // все байты вплоть до следующего заголовка или конца.
    internal class GZipCompressedBlockSupplier : BlockSupplier
    {
        private byte[] _buf;

        public GZipCompressedBlockSupplier(Stream sourceStream, int blockSize) : base(sourceStream, blockSize)
        {
            _buf = new byte[BlockSize];
        }

        public override int Next(out byte[] buf)
        {
            long initPosition = SourceStream.Position;
            
            lock (SourceStream)
            {
                var bytesRead = 1;
                byte x = 0, y = 0, z = 0;
                var nextPart = new List<byte>();

                while (bytesRead > 0)
                {
                    bytesRead = SourceStream.Read(_buf, 0, _buf.Length);

                    for (int i = 0; i < bytesRead; i++)
                    {
                        z = _buf[i];
                        if (x == 31 && y == 139 && z == 8)
                        {
                            if (nextPart.Count > 2)
                            {
                                buf = nextPart.ToArray();
                                SourceStream.Position = initPosition + buf.Length;
                                return buf.Length;
                            }
                            nextPart = new List<byte> { x };
                        }
                        else
                        {
                            nextPart.Add(x);
                        }
                        x = y;
                        y = z;
                    }
                }

                if (nextPart.Count > 0)
                {
                    nextPart.Add(y);
                    nextPart.Add(z);
                }
                buf = nextPart.ToArray();
                SourceStream.Position = initPosition + buf.Length;
                return buf.Length;
            }
        }
    }
}
