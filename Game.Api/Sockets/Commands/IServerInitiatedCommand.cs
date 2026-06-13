namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Marks a socket command as server-initiated only: it is dispatched solely through the Redis
    /// backplane (<see cref="Game.Api.Services.SocketManagerService"/> → the pub/sub command processor)
    /// and must never be invoked by an inbound client message. The inbound path
    /// (<see cref="Sockets.SocketHandler"/>) rejects any command carrying this marker, closing off the
    /// foot-gun where a client could trigger a push command by sending its name — while the
    /// backplane-delivered path, which calls the command directly, still dispatches it.
    /// </summary>
    public interface IServerInitiatedCommand { }
}
