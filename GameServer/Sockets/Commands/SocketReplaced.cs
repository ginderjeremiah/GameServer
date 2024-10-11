using GameServer.Models.Common;

namespace GameServer.Sockets.Commands
{
    public class SocketReplaced : AbstractSocketCommand
    {
        public override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context)
        {
            await context.Close(ESocketCloseReason.SocketReplaced);
            return new();
        }
    }

    public class SocketReplacedInfo : SocketCommandInfo
    {
        public SocketReplacedInfo(string id)
        {
            Name = nameof(SocketReplaced);
            Id = id;
        }
    }
}
