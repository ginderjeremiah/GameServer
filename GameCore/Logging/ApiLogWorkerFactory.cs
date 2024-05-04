using GameCore.Logging.Interfaces;

namespace GameCore.Logging
{
    internal static class ApiLogWorkerFactory
    {
        private static readonly object _lock = new();
        private static ApiLogWorker? _worker;

        public static ApiLogWorker GetWorker(IApiLogger logger)
        {
            if (_worker is null)
            {
                lock (_lock)
                {
                    _worker ??= new ApiLogWorker(logger);
                }
            }
            return _worker;
        }
    }
}
