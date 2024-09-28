using GameServer.Models.Common;

namespace GameServer.Sockets.Commands
{
    public class SocketReplaced : AbstractSocketCommand
    {
        public override ApiSocketResponse Execute()
        {
            return Close(ESocketCloseReason.SocketReplaced);
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
