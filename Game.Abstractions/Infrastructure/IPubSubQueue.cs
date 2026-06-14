namespace Game.Abstractions.Infrastructure
{
    public interface IPubSubQueue
    {
        public string? GetNext();
        public T? GetNext<T>();
        public Task<string?> GetNextAsync();
        public Task<T?> GetNextAsync<T>();
        public void AddToQueue(string value);
        public void AddToQueue<T>(T value);
        public Task AddToQueueAsync(string value);
        public Task AddToQueueAsync<T>(T value);

        /// <summary>
        /// Pushes multiple values onto the queue in a single round-trip (one multi-value LPUSH),
        /// preserving their order. The caller is responsible for not passing an empty sequence.
        /// Named distinctly from <see cref="AddToQueueAsync{T}(T)"/> so passing a concrete
        /// collection (e.g. <c>string[]</c>) can't bind to the single-value generic overload instead.
        /// </summary>
        public Task AddRangeToQueueAsync(IEnumerable<string> values);
    }
}
