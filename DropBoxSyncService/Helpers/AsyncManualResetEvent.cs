using System.Threading;
using System.Threading.Tasks;

namespace DropboxIndexingService.Helpers
{
    /// <summary>
    /// Async reset event is used as a barrier for concurrent tasks.
    /// </summary>
    public class AsyncManualResetEvent
    {
        private volatile TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

        public AsyncManualResetEvent(bool set = false)
        {
            if (set) Set();
        }

        /// <summary>
        /// Creates wait task which is completed when the event is reset
        /// </summary>
        public Task WaitAsync()
        {
            return _tcs.Task;
        }

        /// <summary>
        /// Reset event, effectively completing all wait tasks and releasing pending operations
        /// </summary>
        public void Set()
        {
            _tcs.TrySetResult(true);
        }

#pragma warning disable 420
        public void Reset()
        {
            var tcs = _tcs;
            if (tcs.Task.IsCompleted)
                Interlocked.CompareExchange(ref _tcs, new TaskCompletionSource<bool>(), tcs);
        }
#pragma warning restore 420
    }

}
