namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Marks a socket command that sends its own response instead of letting the command runner send it
    /// (<see cref="Sockets.SocketHandler"/>) — e.g. <see cref="SocketReplaced"/>, whose success frame must
    /// reach the client strictly before the close frame it also sends. The runner skips its own unconditional
    /// send for a self-delivering command and reports <see cref="Sockets.SocketCommandOutcome.Succeeded"/>
    /// unconditionally, rather than reclassifying the runner's own guaranteed-to-fail follow-up send (the
    /// socket the command just closed is no longer Open) as <see cref="Sockets.SocketCommandOutcome.NotDelivered"/>
    /// (#1636).
    /// </summary>
    public interface ISelfDeliveringCommand { }
}
