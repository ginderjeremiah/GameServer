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
                ESocketCloseReason.ServerShuttingDown => "The socket has been closed because the server is shutting down.",
                _ => "The socket has been closed normally."
            };
        }

        public static ModelMapper<TEntity> To<TEntity>(this IEnumerable<TEntity>? source)
        {
            return new ModelMapper<TEntity>(source);
        }
    }
}
