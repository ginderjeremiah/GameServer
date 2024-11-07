using Microsoft.Extensions.Logging;

namespace Game.Core
{
    public class BackgroundWorker
    {
        private readonly AutoResetEvent _resetEvent = new(false);
        private readonly ILogger<BackgroundWorker> _logger;
        private readonly string _uniqueId = Guid.NewGuid().ToString();
        private readonly RegisteredWaitHandle _workerHandle;
        private bool _hasBeenKilled = false;

        public string Name { get; set; } = "Unset";
        public bool IsRunning { get; private set; } = false;

        private BackgroundWorker(ILogger<BackgroundWorker> logger)
        {
            _logger = logger;
        }

        public BackgroundWorker(ILogger<BackgroundWorker> logger, Action<ILogger> action) : this(logger)
        {
            _workerHandle = RegisterWorker(CreateWorkerLoop(() => action(logger)));
        }

        public BackgroundWorker(ILogger<BackgroundWorker> logger, Action action) : this(logger)
        {
            _workerHandle = RegisterWorker(CreateWorkerLoop(action));
        }
        public BackgroundWorker(ILogger<BackgroundWorker> logger, Func<ILogger, Task> action) : this(logger)
        {
            _workerHandle = RegisterWorker(CreateAsyncWorkerLoop(async () => await action(logger)));
        }

        public BackgroundWorker(ILogger<BackgroundWorker> logger, Func<Task> action) : this(logger)
        {
            _workerHandle = RegisterWorker(CreateAsyncWorkerLoop(action));
        }

        public void Start()
        {
            if (_hasBeenKilled)
            {
                _logger.LogError($"The background worker '{Name}' ({_uniqueId}) has been killed and cannot be started.");
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
