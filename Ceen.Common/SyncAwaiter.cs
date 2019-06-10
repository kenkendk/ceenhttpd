using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Ceen.Common
{
    public sealed class SyncAwaiter : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback callback, object state)> m_queue = new BlockingCollection<(SendOrPostCallback, object)>();

        private SyncAwaiter() { }

        public static void WaitSync(Func<Task> asyncOperation)
        {
            var prevContext = Current;

            SyncAwaiter sync = null;
            try
            {
                sync = new SyncAwaiter();
                SetSynchronizationContext(sync);

                var awaiter = asyncOperation().GetAwaiter();
                sync.ExecuteCallbacks(awaiter);

                // handle non-success result
                awaiter.GetResult();
            }
            finally
            {
                SetSynchronizationContext(prevContext);
                sync?.Dispose();
            }
        }

        private void ExecuteCallbacks(TaskAwaiter awaiter)
        {
            while (!awaiter.IsCompleted)
            {
                var item = m_queue.Take();
                item.callback(item.state);
            }
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            m_queue.Add((d, state));
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            if (Current == this)
            {
	            // already on execution thread
                d(state);
                return;
            }

            var task = new Task(new Action<object>(d), state);
            m_queue.Add((x => ((Task) x).RunSynchronously(), task));

            task.Wait();
        }

        public override SynchronizationContext CreateCopy()
        {
            return this;
        }

        public void Dispose()
        {
            m_queue.Dispose();
        }
    }
}