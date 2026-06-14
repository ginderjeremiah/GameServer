using Game.Api.Models;

namespace Game.Api
{
    public static class Extensions
    {
        public static string GetDescription(this ESocketCloseReason reason)
        {
            return reason switch
            {
                ESocketCloseReason.Finished => "The socket has been closed normally.",
                ESocketCloseReason.Inactivity => "The socket has been closed due to inactivity.",
                ESocketCloseReason.SocketReplaced => "The socket has been closed because a new one was established.",
                ESocketCloseReason.MessageTooBig => "The socket has been closed because a message exceeded the maximum allowed size.",
                ESocketCloseReason.ServerShuttingDown => "The socket has been closed because the server is shutting down.",
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unhandled socket close reason.")
            };
        }

        public static ModelMapper<TEntity> To<TEntity>(this IEnumerable<TEntity>? source)
        {
            return new ModelMapper<TEntity>(source);
        }
    }
}
