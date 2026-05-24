using Microsoft.Extensions.DependencyInjection;

namespace Game.DataAccess
{
    /// <summary>
    /// A cache for an array of data that is loaded via a scoped service provider.
    /// </summary>
    /// <typeparam name="T">The type of item stored in the cache.</typeparam>
    public class ArrayDataCache<T>
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Func<IServiceProvider, Task<List<T>>> _getter;

        /// <summary>
        /// The data loaded into the cache.
        /// </summary>
        public List<T> Data { get; set; } = [];

        /// <summary>
        /// Default constructor. Automatically begins loading the data into the cache.
        /// </summary>
        /// <param name="serviceProvider">The root service provider used to create a scope for each reload.</param>
        /// <param name="getter">Factory that resolves services from the scoped provider and returns data.</param>
        public ArrayDataCache(IServiceProvider serviceProvider, Func<IServiceProvider, Task<List<T>>> getter)
        {
            _serviceProvider = serviceProvider;
            _getter = getter;
            Task.Run(ReloadData);
        }

        /// <summary>
        /// Reloads the data in the cache using a fresh service scope.
        /// </summary>
        public async Task ReloadData()
        {
            using var scope = _serviceProvider.CreateScope();
            Data = await _getter(scope.ServiceProvider);
        }
    }
}
