using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace GZipTest
{
    // Класс-шаблон алгоритма для многопоточных преобразований массивов байтов.
    // Полагает (вполне справедливо), что трансформация занимает больше времени,
    // чем поставка или потребления, поэтому использует одного поставщика,
    // одного потребителя и нескольких рабочих.
    // Гарантирует, что данные будут переданы потребителю в том порядке, в
    // котором они поступили от поставщика.
    // Поддерживает отмену и механизм сообщения об ошибках.
    class ParallelByteArrayTransformer
    {
        public delegate byte[] TransformMethod(byte[] buf);
        public delegate void ConsumeMethod(byte[] buf);

        private bool _aborting;

        private Exception _exception;
        public Exception Exception
        {
            get
            {
                return _exception;
            }
            private set
            {
                _exception = value;
            }
        }

        private int _processorCount;

        private AutoResetEvent[] _processorReady;
        private AutoResetEvent[] _dataSupplied;
        private AutoResetEvent[] _consumerReady;
        private AutoResetEvent[] _dataProcessed;

        private byte[][] _bufs1;
        private byte[][] _bufs2;

        private struct _aux
        {
            public TransformMethod TransformMethod;
            public int Id;
        }

        public void Cancel()
        {
            _aborting = true;
            _exception = new OperationCanceledException("Operation was cancelled by request from user.");
        }

        public ParallelByteArrayTransformer()
        {
            _processorCount = 1;// Environment.ProcessorCount;

            _processorReady = new AutoResetEvent[_processorCount];
            _dataSupplied = new AutoResetEvent[_processorCount];
            _consumerReady = new AutoResetEvent[_processorCount];
            _dataProcessed = new AutoResetEvent[_processorCount];

            for (int i = 0; i < _processorCount; i++)
            {
                _processorReady[i] = new AutoResetEvent(false);
                _dataSupplied[i] = new AutoResetEvent(false);
                _consumerReady[i] = new AutoResetEvent(false);
                _dataProcessed[i] = new AutoResetEvent(false);
            }

            _bufs1 = new byte[_processorCount][];
            _bufs2 = new byte[_processorCount][];
        }

        public bool Transform(BlockSupplier blockSupplier, TransformMethod transformMethod, ConsumeMethod consumeMethod)
        {
            for (int i = 0; i < _processorCount; i++)
            {
                _processorReady[i].Reset();
                _dataSupplied[i].Reset();
                _consumerReady[i].Reset();
                _dataProcessed[i].Reset();
            }

            var threadList = new List<Thread>();

            var supplier = new Thread(Supply) { Name = "Supplier", IsBackground = true, Priority = ThreadPriority.AboveNormal };
            threadList.Add(supplier);
            supplier.Start(blockSupplier);

            var processors = new Thread[_processorCount];
            for (int i = 0; i < _processorCount; i++)
            {
                processors[i] = new Thread(Process)
                    { Name = $"worker {i}", IsBackground = true, Priority = ThreadPriority.AboveNormal };
                threadList.Add(processors[i]);
                processors[i].Start(new _aux { Id = i, TransformMethod = transformMethod });
            }

            var consumer = new Thread(Consume) { Name = "Writer", IsBackground = true, Priority = ThreadPriority.AboveNormal };
            threadList.Add(consumer);
            consumer.Start(consumeMethod);
            
            supplier.Join();
            for (int i = 0; i < processors.Length; i++)
            {
                processors[i].Join();
            }
            consumer.Join();

            return _exception == null ? true : false;
        }

        CleverQueue queue1 = new CleverQueue(6);
        CleverQueue queue2 = new CleverQueue(6);

        private void Supply(object o)
        {
            BlockSupplier supp = (BlockSupplier)o;

            try
            {
                int bytesSupplied = 1;
                var partNo = 0;

                while (bytesSupplied > 0)
                {
                    bytesSupplied = supp.Next(out byte[] buf);
                    while (!queue1.TryEnqueue(buf, partNo++) && !_aborting);
                }
            }
            catch (Exception e)
            {
                _exception = e;
                _aborting = true;
            }
        }

        private void Process(object o)
        {
            var aux = (_aux)o;
            TransformMethod transform = aux.TransformMethod;
            int id = aux.Id;

            byte[] buf, bufe;
            int blockID;

            try
            {
                while (true)
                {
                    while (!queue1.TryDequeue(out bufe, out blockID) && !_aborting);
                    if (_aborting) break;
                    
                    if (bufe.Length == 0)
                    {
                        _aborting = true;
                        break;
                    }

                    buf = transform(bufe);

                    while (!queue2.TryEnqueue(buf, blockID) && !_aborting);
                }
            }
            catch (Exception e)
            {
                _exception = e;
                _aborting = true;
            }
        }

        private void Consume(object o)
        {
            ConsumeMethod consume = (ConsumeMethod)o;
            try
            {
                byte[] buf;
                while (true)
                {
                    while (!queue2.TryDequeue(out buf, out int id) && !_aborting);
                    if (_aborting) break;
                    consume(buf);
                }
            }
            catch (Exception e)
            {
                _exception = e;
                _aborting = true;
            }
        }
    }
}
