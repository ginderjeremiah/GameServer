using Game.Api.Models.Common;

namespace Game.Api.Sockets.Commands
{
    public class SocketReplaced : AbstractSocketCommand, IServerInitiatedCommand, ISelfDeliveringCommand
    {
        public override string Name { get; set; } = nameof(SocketReplaced);

        public override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            // This command deliberately owns its own send-then-close (marked via ISelfDeliveringCommand)
            // rather than deferring the send to the command runner: the success frame is what the client's
            // SocketReplaced handler reacts to, and it must reach the client BEFORE the close frame — but the
            // runner sends only after this returns, which is too late. Close() then moves the socket out of
            // Open; ISelfDeliveringCommand tells the runner to skip its own follow-up send rather than attempt
            // one that's guaranteed to fail on the now-closed socket and get misclassified as a delivery
            // failure (#1636). It is returned as Success() (not an empty frame) purely defensively: were the
            // ordering ever changed so the socket were still Open, the runner would emit a valid success frame
            // rather than a spurious empty one.
            await context.SendData(Success());
            await context.Close(ESocketCloseReason.SocketReplaced);
            return Success();
        }
    }

    public class SocketReplacedInfo : SocketCommandInfo
    {
        public SocketReplacedInfo() : base(nameof(SocketReplaced)) { }
    }
}
