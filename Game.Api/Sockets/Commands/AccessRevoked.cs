using Game.Api.Models.Common;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Pushed to a player's live socket when an admin action (ban/archive) revokes the account's access —
    /// so the loss of access takes effect immediately rather than only once the client's access token
    /// expires or the socket next goes idle. Mirrors <see cref="SocketReplaced"/>'s send-then-close shape.
    /// </summary>
    public class AccessRevoked : AbstractSocketCommand, IServerInitiatedCommand, ISelfDeliveringCommand
    {
        public override string Name { get; set; } = nameof(AccessRevoked);

        public override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            // Same ordering rationale as SocketReplaced: the client's AccessRevoked handler reacts to this
            // success frame, so it must be sent before the close frame.
            await context.SendData(Success());
            await context.Close(ESocketCloseReason.AccessRevoked);
            return Success();
        }
    }

    public class AccessRevokedInfo : SocketCommandInfo
    {
        public AccessRevokedInfo() : base(nameof(AccessRevoked)) { }
    }
}
