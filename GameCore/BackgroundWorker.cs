namespace GameCore
{
    public class BackgroundWorker
    {
        private readonly AutoResetEvent _resetEvent = new(false);
        private readonly CancellationTokenSource _cancelSource = new();
        private readonly IApiLogger _logger;
        private readonly CancellationToken _cancelToken;
        private readonly string _uniqueId = Guid.NewGuid().ToString();

        public string Name { get; set; } = "Unknown";
        public bool IsRunning { get; private set; } = false;

        public BackgroundWorker(IApiLogger logger, Action<IApiLogger> action, CancellationToken? cancelToken = null)
        {
            _logger = logger;
            _cancelToken = cancelToken ?? CancellationToken.None;
            Task.Run(CreateWorkerLoop(() => action(logger)), _cancelToken);
        }

        public BackgroundWorker(IApiLogger logger, Action action, CancellationToken? cancelToken = null)
        {
            _logger = logger;
            _cancelToken = cancelToken ?? CancellationToken.None;
            Task.Run(CreateWorkerLoop(action), _cancelToken);
        }
        public BackgroundWorker(IApiLogger logger, Func<IApiLogger, Task> action, CancellationToken? cancelToken = null)
        {
            _logger = logger;
            _cancelToken = cancelToken ?? CancellationToken.None;
            Task.Run(CreateAsyncWorkerLoop(async () => await action(logger)), _cancelToken);
        }

        public BackgroundWorker(IApiLogger logger, Func<Task> action, CancellationToken? cancelToken = null)
        {
            _logger = logger;
            _cancelToken = cancelToken ?? CancellationToken.None;
            Task.Run(CreateAsyncWorkerLoop(action), _cancelToken);
        }

        public void Start()
        {
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
            _cancelSource.Cancel();
        }

        private Action CreateWorkerLoop(Action loopAction)
        {
            return () =>
            {
                using var source = CancellationTokenSource.CreateLinkedTokenSource(_cancelToken, _cancelSource.Token);
                do
                {
                    if (_resetEvent.WaitOne(5000))
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
                    }
                }
                while (!source.Token.IsCancellationRequested);
            };
        }

        private Func<Task> CreateAsyncWorkerLoop(Func<Task> loopAction)
        {
            return async () =>
            {
                using var source = CancellationTokenSource.CreateLinkedTokenSource(_cancelToken, _cancelSource.Token);
                do
                {
                    if (_resetEvent.WaitOne(5000))
                    {
                        IsRunning = true;
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
                    }
                }
                while (!source.IsCancellationRequested);
            };
        }
    }
}
