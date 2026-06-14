namespace Game.Api
{
    public enum ESocketCloseReason
    {
        Finished = 0,
        Inactivity = 1,
        SocketReplaced = 2,
        MessageTooBig = 3,
        ServerShuttingDown = 4
    }
}
