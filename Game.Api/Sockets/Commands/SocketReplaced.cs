using Game.Api.Models.Common;

namespace Game.Api.Sockets.Commands
{
    public class SocketReplaced : AbstractSocketCommand
    {
        public override string Name { get; set; } = nameof(SocketReplaced);

        public override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context)
        {
            await context.SendData(Success());
            await context.Close(ESocketCloseReason.SocketReplaced);
            return new();
        }
    }

    public class SocketReplacedInfo : SocketCommandInfo
    {
        public SocketReplacedInfo() : base(nameof(SocketReplaced)) { }
    }
}
