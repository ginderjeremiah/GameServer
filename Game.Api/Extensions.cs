using Game.Api.Models;

namespace Game.Api
{
    public static class Extensions
    {
        public static string GetDescription(this ESocketCloseReason reason)
        {
            return reason switch
            {
                ESocketCloseReason.Inactivity => "The socket has been closed due to inactivity.",
                ESocketCloseReason.SocketReplaced => "The socket has been closed because a new one was established.",
                _ => "The socket has been closed normally."
            };
        }

        public static AsyncModelMapper<TEntity> To<TEntity>(this IAsyncEnumerable<TEntity>? source)
        {
            return new AsyncModelMapper<TEntity>(source);
        }

        public static ModelMapper<TEntity> To<TEntity>(this IEnumerable<TEntity>? source)
        {
            return new ModelMapper<TEntity>(source);
        }

        public static async IAsyncEnumerable<TResult> Select<TSource, TResult>(this IAsyncEnumerable<TSource> source, Func<TSource, TResult> selector)
        {
            await foreach (var item in source)
            {
                yield return selector(item);
            }
        }
    }
}
