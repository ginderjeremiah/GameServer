using Microsoft.Extensions.Logging;

namespace Game.Infrastructure
{
    /// <summary>
    /// A lightweight container for managing and repeatedly executing a delegate using a background <see cref="Task"/>.
    /// <para>
    /// <see cref="Kill"/> and <see cref="Dispose"/> are distinct: <see cref="Kill"/> stops the worker from being
    /// scheduled again while leaving the instance queryable (e.g. <see cref="IsRunning"/>), whereas
    /// <see cref="Dispose"/> additionally releases the underlying OS wait handle and must be called once the worker
    /// is no longer needed. <see cref="Dispose"/> implies <see cref="Kill"/>.
    /// </para>
    /// </summary>
    public class BackgroundWorker : IDisposable
    {
        private readonly AutoResetEvent _resetEvent = new(false);
        private readonly ILogger<BackgroundWorker> _logger;
        private readonly string _uniqueId = Guid.NewGuid().ToString();
        private readonly RegisteredWaitHandle _workerHandle;

        // Teardown flags written by Kill()/Dispose() and read by Start(), potentially from different threads, so
        // they are volatile to publish the write promptly: without it a Start() racing a Dispose() could read a
        // stale _disposed == false and then Set() an already-disposed _resetEvent.
        private volatile bool _hasBeenKilled = false;
        private volatile bool _disposed = false;

        // Written on the Start() caller's thread and on the thread-pool worker-loop thread, and read from
        // arbitrary threads, so it is volatile to guarantee writes are published and reads observe transitions
        // promptly rather than relying on a non-synchronized field.
        private volatile bool _isRunning = false;

        /// <summary>
        /// A custom name for the <see cref="BackgroundWorker"/> used for logging purposes.
        /// </summary>
        public string Name { get; set; } = "Unset";

        /// <summary>
        /// True when the <see cref="BackgroundWorker"/> is actively processing its action, otherwise false.
        /// </summary>
        public bool IsRunning
        {
            get => _isRunning;
            private set => _isRunning = value;
        }

        /// <summary>
        /// Initializes the <see cref="BackgroundWorker"/> with a synchronous delegate expecting no parameters.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="action"></param>
        public BackgroundWorker(ILogger<BackgroundWorker> logger, Action action)
        {
            _logger = logger;
            _workerHandle = RegisterWorker(CreateWorkerLoop(action));
        }

        /// <summary>
        /// Initializes the <see cref="BackgroundWorker"/> with an asynchronous delegate expecting no parameters.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="action"></param>
        public BackgroundWorker(ILogger<BackgroundWorker> logger, Func<Task> action)
        {
            _logger = logger;
            _workerHandle = RegisterWorker(CreateAsyncWorkerLoop(action));
        }

        /// <summary>
        /// Queues the delegate to be run again.
        /// <para>Will throw an <see cref="InvalidOperationException"/> if the <see cref="BackgroundWorker"/> has been killed using <see cref="Kill"/>,
        /// or an <see cref="ObjectDisposedException"/> if it has been disposed.</para>
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="ObjectDisposedException"></exception>
        public void Start()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_hasBeenKilled)
            {
                _logger.LogError("The background worker '{Name}' ({UniqueId}) has been killed and cannot be started.", Name, _uniqueId);
                throw new InvalidOperationException("The background worker has been killed and cannot be started.");
            }

            // Publish IsRunning = true before signaling the worker loop. The loop's trailing IsRunning = false
            // therefore always happens-after this write (the Set() / wait pairing establishes the ordering), so a
            // fast run can never be left wedged 'true' by a late write — the bug this ordering guards against.
            IsRunning = true;
            if (_resetEvent.Set())
            {
                _logger.LogTrace("Starting background worker '{Name}' ({UniqueId}).", Name, _uniqueId);
            }
            else
            {
                // Signaling failed, so no run was queued; undo the optimistic running state.
                IsRunning = false;
                _logger.LogError("Failed to start background worker '{Name}' ({UniqueId}).", Name, _uniqueId);
            }
        }

        /// <summary>
        /// Prevents the delegate from being queued again for execution. Does not cancel the delegate if in progress.
        /// </summary>
        public void Kill()
        {
            _hasBeenKilled = true;
            // Pass null rather than the reset event: nothing waits on it once unregistered, and signaling it on
            // completion would race a concurrent Dispose that releases the same handle.
            _workerHandle.Unregister(null);
        }

        /// <summary>
        /// Stops the worker from being scheduled again (see <see cref="Kill"/>) and releases the underlying OS wait handle.
        /// Safe to call more than once. Like <see cref="Kill"/>, this does not cancel a delegate already in progress.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            if (!_hasBeenKilled)
            {
                Kill();
            }

            // The worker loop never touches _resetEvent, and once Unregister has returned the thread pool no longer
            // waits on it, so disposing it here is safe even if a delegate is still in progress.
            _resetEvent.Dispose();
        }

        private RegisteredWaitHandle RegisterWorker(WaitOrTimerCallback callback)
        {
            return ThreadPool.RegisterWaitForSingleObject(_resetEvent, callback, null, -1, false);
        }

        private WaitOrTimerCallback CreateWorkerLoop(Action loopAction)
        {
            return (state, timedOut) =>
            {
                try
                {
                    loopAction();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occured while creating the worker loop.");
                }

                IsRunning = false;
                _logger.LogTrace("Sleeping background worker '{Name}' ({UniqueId}).", Name, _uniqueId);
            };
        }

        private WaitOrTimerCallback CreateAsyncWorkerLoop(Func<Task> loopAction)
        {
            return async (state, timedOut) =>
            {
                try
                {
                    await loopAction();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occured while creating the async worker loop.");
                }

                IsRunning = false;
                _logger.LogTrace("Sleeping background worker '{Name}' ({UniqueId}).", Name, _uniqueId);
            };
        }
    }
}
