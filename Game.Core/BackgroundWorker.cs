using Microsoft.Extensions.Logging;

namespace Game.Core
{
    /// <summary>
    /// A lightweight container for managing and repeatedly executing a delegate using a background <see cref="Task"/>.
    /// </summary>
    public class BackgroundWorker
    {
        private readonly AutoResetEvent _resetEvent = new(false);
        private readonly ILogger<BackgroundWorker> _logger;
        private readonly string _uniqueId = Guid.NewGuid().ToString();
        private readonly RegisteredWaitHandle _workerHandle;
        private bool _hasBeenKilled = false;

        /// <summary>
        /// A custom name for the <see cref="BackgroundWorker"/> used for logging purposes.
        /// </summary>
        public string Name { get; set; } = "Unset";

        /// <summary>
        /// True when the <see cref="BackgroundWorker"/> is actively processing its action, otherwise false.
        /// </summary>
        public bool IsRunning { get; private set; } = false;

        /// <summary>
        /// Initializes the <see cref="BackgroundWorker"/> with a synchronous delegate expecting an <see cref="ILogger"/> parameter. 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="action"></param>
        public BackgroundWorker(ILogger<BackgroundWorker> logger, Action<ILogger> action)
        {
            _logger = logger;
            _workerHandle = RegisterWorker(CreateWorkerLoop(() => action(logger)));
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
        /// Initializes the <see cref="BackgroundWorker"/> with an asynchronous delegate expecting an <see cref="ILogger"/> parameter. 
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="action"></param>
        public BackgroundWorker(ILogger<BackgroundWorker> logger, Func<ILogger, Task> action)
        {
            _logger = logger;
            _workerHandle = RegisterWorker(CreateAsyncWorkerLoop(async () => await action(logger)));
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
        /// <para>Will throw and <see cref="InvalidOperationException"/> if the <see cref="BackgroundWorker"/> has been killed using <see cref="Kill"/>.</para>
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        public void Start()
        {
            if (_hasBeenKilled)
            {
                _logger.LogError("The background worker '{Name}' ({UniqueId}) has been killed and cannot be started.", Name, _uniqueId);
                throw new InvalidOperationException("The background worker has been killed and cannot be started.");
            }

            if (_resetEvent.Set())
            {
                IsRunning = true;
                _logger.LogTrace("Starting background worker '{Name}' ({UniqueId}).", Name, _uniqueId);
            }
            else
            {
                _logger.LogError("Failed to start background worker '{Name}' ({UniqueId}).", Name, _uniqueId);
            }
        }

        /// <summary>
        /// Prevents the delegate from being queued again for execution. Does not cancel the delegate if in progress.
        /// </summary>
        public void Kill()
        {
            _hasBeenKilled = true;
            _workerHandle.Unregister(_resetEvent);
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
