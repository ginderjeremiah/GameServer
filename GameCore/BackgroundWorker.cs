namespace GameCore
{
    public class BackgroundWorker
    {
        private readonly AutoResetEvent _resetEvent = new(false);
        private readonly IApiLogger _logger;
        private readonly string _uniqueId = Guid.NewGuid().ToString();
        private readonly RegisteredWaitHandle _workerHandle;

        private bool Killed { get; set; } = false;

        public string Name { get; set; } = "Unset";
        public bool IsRunning { get; private set; } = false;

        private BackgroundWorker(IApiLogger logger)
        {
            _logger = logger;
        }

        public BackgroundWorker(IApiLogger logger, Action<IApiLogger> action) : this(logger)
        {
            _workerHandle = RegisterWorker(CreateWorkerLoop(() => action(logger)));
        }

        public BackgroundWorker(IApiLogger logger, Action action) : this(logger)
        {
            _workerHandle = RegisterWorker(CreateWorkerLoop(action));
        }
        public BackgroundWorker(IApiLogger logger, Func<IApiLogger, Task> action) : this(logger)
        {
            _workerHandle = RegisterWorker(CreateAsyncWorkerLoop(async () => await action(logger)));
        }

        public BackgroundWorker(IApiLogger logger, Func<Task> action) : this(logger)
        {
            _workerHandle = RegisterWorker(CreateAsyncWorkerLoop(action));
        }

        public void Start()
        {
            if (Killed)
            {
                _logger.LogError($"The background worker '{Name}' ({_uniqueId}) has been killed and cannot be started.");
                throw new InvalidOperationException("The background worker has been killed and cannot be started.");
            }

            if (_resetEvent.Set())
            {
                IsRunning = true;
                _logger.Log($"Starting background worker '{Name}' ({_uniqueId}).");
            }
            else
            {
                _logger.LogError($"Failed to start background worker '{Name}' ({_uniqueId}).");
            }
        }

        public void Kill()
        {
            Killed = true;
            _workerHandle.Unregister(_resetEvent);
        }

        private RegisteredWaitHandle RegisterWorker(WaitOrTimerCallback callback)
        {
            return ThreadPool.RegisterWaitForSingleObject(_resetEvent, callback, null, -1, false);
        }

        private WaitOrTimerCallback CreateWorkerLoop(Action loopAction)
        {
            return (object? state, bool timedOut) =>
            {
                try
                {
                    loopAction();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex);
                }

                IsRunning = false;
                _logger.Log($"Sleeping background worker '{Name}' ({_uniqueId}).");
            };
        }

        private WaitOrTimerCallback CreateAsyncWorkerLoop(Func<Task> loopAction)
        {
            return async (object? state, bool timedOut) =>
            {
                try
                {
                    await loopAction();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex);
                }

                IsRunning = false;
                _logger.Log($"Sleeping background worker '{Name}' ( {_uniqueId} ).");
            };
        }
    }
}
