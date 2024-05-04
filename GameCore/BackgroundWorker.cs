using GameCore.Logging.Interfaces;

namespace GameCore
{
    internal class BackgroundWorker
    {
        private readonly AutoResetEvent _resetEvent = new(false);
        private readonly IApiLogger _logger;

        public string Name { get; set; } = "Unknown";
        public bool IsRunning { get; private set; } = false;

        public BackgroundWorker(IApiLogger logger, Action<IApiLogger> action)
        {
            _logger = logger;
            Task.Run(() =>
            {
                while (_resetEvent.WaitOne())
                {
                    try
                    {
                        action(logger);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex);
                    }
                    IsRunning = false;
                    _logger.Log($"Sleeping background worker '{Name}'.");
                }
            });
        }

        public BackgroundWorker(IApiLogger logger, Action action)
        {
            _logger = logger;
            Task.Run(() =>
            {
                while (_resetEvent.WaitOne())
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex);
                    }
                    IsRunning = false;
                    _logger.Log($"Sleeping background worker '{Name}'.");
                }
            });
        }

        public void Start()
        {
            if (_resetEvent.Set())
            {
                IsRunning = true;
                _logger.Log($"Starting background worker '{Name}'.");
            }
            else
            {
                _logger.LogError($"Failed to start background worker '{Name}'.");
            }
        }
    }
}
