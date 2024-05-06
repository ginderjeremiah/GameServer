using System.Diagnostics.CodeAnalysis;

namespace GameCore.Infrastructure
{
    public interface IPubSubQueue
    {
        public string? GetNext();
        public T? GetNext<T>();
        public Task<string?> GetNextAsync();
        public Task<T?> GetNextAsync<T>();
        public bool TryGetNext([NotNullWhen(true)] out string? value);
        public bool TryGetNext<T>([NotNullWhen(true)] out T? value);
        public void AddToQueue(string value);
        public void AddToQueue<T>(T value);
        public Task AddToQueueAsync(string value);
        public Task AddToQueueAsync<T>(T value);
    }
}
