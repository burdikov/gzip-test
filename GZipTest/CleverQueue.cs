using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace GZipTest
{
    class CleverQueue
    {
        private int _currentOut = 0;

        private readonly int _capacity;

        private readonly AutoResetEvent _canEnqueue = new AutoResetEvent(true);
        private readonly AutoResetEvent _canDequeue = new AutoResetEvent(false);

        private Dictionary<int, byte[]> _store = new Dictionary<int, byte[]>();

        private Semaphore _semEnqueue, _semDequeue;

        public CleverQueue(int capacity)
        {
            _capacity = capacity;
            _semEnqueue = new Semaphore(_capacity, _capacity);
            _semDequeue = new Semaphore(0, _capacity);
        }

        public bool TryEnqueue(byte[] buf, int id)
        {
            if (_semEnqueue.WaitOne())
            {
                lock (_store)
                {
                    _store.Add(id, buf);
                    _semDequeue.Release();
                    return true;
                }
            }
            else
            {
                return false;
            }
        }


        public bool TryDequeue(out byte[] buf, out int id)
        {
            var local = _currentOut;
            buf = null;
            id = -1;

            if (_semDequeue.WaitOne())
            {
                lock (_store)
                    if (_store.TryGetValue(local, out buf))
                    {
                        id = local;
                        _currentOut++;
                        _store.Remove(local);
                        _semEnqueue.Release();
                        return true;
                    }
            }

            return false;
        }

    }
}
