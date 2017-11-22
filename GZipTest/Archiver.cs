using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace GZipTest
{
    //Класс, представляющий собой архиватор

    public abstract class Archiver
    {
        public Exception Exception { get; protected set; }

        public abstract bool Compress(Stream source, Stream destination);

        public abstract bool Decompress(Stream source, Stream destination);

        public abstract void Cancel();
    }

    // Класс-архиватор, который по сути является адаптером к классу ParallelByteArrayTransformer
    // и позволяет использовать его для упаковки/распаковки потоков байтов.
    // Поддерживает отмену и может сообщить об ошибках, возникших в ходе работы (через поле).
    // Содержит запасной алгоритм распаковки файлов (однопоточный) на случай неудачи
    // многопоточного алгоритма.
    public class ParallelGZipArchiver : Archiver
    {
        private ParallelByteArrayTransformer _transformer = new ParallelByteArrayTransformer();

        public override bool Compress(Stream source, Stream destination)
        {
            var blockSupplier = new NonCompressedBlockSupplier(source, 65536);

            ParallelByteArrayTransformer.ConsumeMethod consumeMethod = 
                (byte[] buf) => destination.Write(buf, 0, buf.Length);

            ParallelByteArrayTransformer.TransformMethod compressMethod =
                (byte[] buf) =>
                {
                    using (var ms = new MemoryStream())
                    {
                        using (var gzip = new GZipStream(ms, CompressionMode.Compress))
                        {
                            gzip.Write(buf, 0, buf.Length);
                        }
                        return ms.ToArray();
                    }
                };

            if (_transformer.Transform(blockSupplier, compressMethod, consumeMethod))
                return true;
            else
            {
                Exception = _transformer.Exception;
                return false;
            }
        }

        public override bool Decompress(Stream source, Stream destination)
        {
            long _srcpos = source.Position;
            long _dstpos = destination.Position;

            var blockSupplier = new GZipCompressedBlockSupplier(source, 65536);

            ParallelByteArrayTransformer.ConsumeMethod consumeMethod =
                (byte[] buf) => destination.Write(buf, 0, buf.Length);

            ParallelByteArrayTransformer.TransformMethod transformMethod =
                (byte[] buf) =>
                {
                    using (var ms = new MemoryStream(buf))
                    using (var gzip = new GZipStream(ms, CompressionMode.Decompress))
                    {
                        var outbuf = new byte[ms.Length * 2];
                        int bytesRead = 1, offset = 0;

                        while (bytesRead > 0)
                        {
                            bytesRead = gzip.Read(outbuf, offset, outbuf.Length - offset);
                            offset += bytesRead;

                            if (offset == outbuf.Length) Array.Resize(ref outbuf, outbuf.Length * 2);
                        }

                        Array.Resize(ref outbuf, offset);
                        return outbuf;
                    }
                };

            if (_transformer.Transform(blockSupplier, transformMethod, consumeMethod))
                return true;
            else
            {
                var e = _transformer.Exception;

                if (e is IOException ||
                    e is InvalidOperationException ||
                    e is ObjectDisposedException)
                {
                    Exception = _transformer.Exception;
                    return false;
                }

                source.Position = _srcpos;
                destination.Position = _dstpos;

                return BackupDecompress(source, destination);
            }
        }

        private bool BackupDecompress(Stream source, Stream destination)
        {
            try
            {
                var buf = new byte[65536];
                var bytesRead = 1;

                using (var gzip = new GZipStream(source, CompressionMode.Decompress, true))
                {
                    while (bytesRead > 0)
                    {
                        bytesRead = gzip.Read(buf, 0, buf.Length);
                        destination.Write(buf, 0, bytesRead);
                    }
                }
                return true;
            }
            catch (Exception e)
            {
                Exception = e;
                return false;
            }
        }

        public override void Cancel()
        {
            _transformer.Cancel();
        }
    }
}
