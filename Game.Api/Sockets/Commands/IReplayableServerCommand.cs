namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Opts a server-initiated command (<see cref="IServerInitiatedCommand"/>) into dead-letter replay
    /// eligibility. A session-lifecycle signal (e.g. <see cref="SocketReplaced"/>) is only meaningful at the
    /// moment it was originally emitted — redelivering it later, against whatever socket is currently live
    /// for the player, is never correct — so replay eligibility is opt-in rather than following automatically
    /// from <see cref="IServerInitiatedCommand"/>.
    /// </summary>
    public interface IReplayableServerCommand : IServerInitiatedCommand { }
}
