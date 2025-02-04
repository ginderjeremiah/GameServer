using Game.Abstractions.DataAccess;
using Microsoft.Extensions.DependencyInjection;

namespace Game.DataAccess
{
    /// <summary>
    /// A cache for an array of data.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ArrayDataCache<T>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Func<IRepositoryManager, Task<List<T>>> _getter;

        /// <summary>
        /// The data loaded into the cache.
        /// </summary>
        public List<T> Data { get; set; } = [];

        /// <summary>
        /// Default constructor. Automatically beings loading the data into the cache.
        /// </summary>
        /// <param name="getter"></param>
        /// <param name="serviceProvider"></param>
        public ArrayDataCache(IServiceProvider serviceProvider, Func<IRepositoryManager, Task<List<T>>> getter)
        {
            _serviceProvider = serviceProvider;
            _getter = getter;
            Task.Run(ReloadData);
        }

        /// <summary>
        /// Reloads the data in the cache.
        /// </summary>
        /// <returns></returns>
        public async Task ReloadData()
        {
            _serviceProvider.CreateScope();
            Data = await _getter(_serviceProvider.GetRequiredService<IRepositoryManager>());
        }
    }
}
