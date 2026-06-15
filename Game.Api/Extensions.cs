using Game.Api.Models;
using System.Net.WebSockets;

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

        /// <summary>
        /// Maps a close reason to the WebSocket status code sent on the close frame, so a client inspecting
        /// the status code alone can tell an error/abnormal closure from a graceful one. Graceful, expected
        /// closures use <see cref="WebSocketCloseStatus.NormalClosure"/>: a finished connection, an intentional
        /// single-session takeover, and a planned shutdown drain (the client treats that normal closure as a
        /// cue to reconnect to a healthy instance). Non-graceful closures get a distinct status — an idle
        /// timeout as a policy violation, an oversized message as the matching message-too-big code.
        /// </summary>
        public static WebSocketCloseStatus GetCloseStatus(this ESocketCloseReason reason)
        {
            return reason switch
            {
                ESocketCloseReason.Finished => WebSocketCloseStatus.NormalClosure,
                ESocketCloseReason.SocketReplaced => WebSocketCloseStatus.NormalClosure,
                ESocketCloseReason.ServerShuttingDown => WebSocketCloseStatus.NormalClosure,
                ESocketCloseReason.Inactivity => WebSocketCloseStatus.PolicyViolation,
                ESocketCloseReason.MessageTooBig => WebSocketCloseStatus.MessageTooBig,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unhandled socket close reason.")
            };
        }

        public static ModelMapper<TEntity> To<TEntity>(this IEnumerable<TEntity>? source)
        {
            return new ModelMapper<TEntity>(source);
        }
    }
}
