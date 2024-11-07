using Game.Api.Models;
using Game.Api.Models.Player;
using Game.Core.Sessions;

namespace Game.Api
{
    /// <summary>
    /// A class to house generic extensions used within the project.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Creates a <see cref="PlayerData"/> object from a <see cref="Session"/>.
        /// </summary>
        /// <param name="session"></param>
        /// <returns>A <see cref="PlayerData"/> object.</returns>
        public static PlayerData GetPlayerData(this Session session)
        {
            return new(session.Player, session.InventoryData, session.CurrentZone);
        }

        public static string GetDescription(this ESocketCloseReason reason)
        {
            return reason switch
            {
                ESocketCloseReason.Inactivity => "The socket has been closed due to inactivity.",
                ESocketCloseReason.SocketReplaced => "The socket has been closed because a new one was established.",
                //ESocketCloseReason.Finished
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
