namespace Game.Api.Models.Common
{
    /// <summary>
    /// Payload of the server-initiated <see cref="Sockets.Commands.ServerCommandFailed"/> notice: the name
    /// of the server-pushed command that failed on the server (after being dead-lettered), so the client can
    /// re-sync the authoritative state that push would have updated instead of silently diverging (#671).
    /// </summary>
    public class ServerCommandFailedModel
    {
        public required string CommandName { get; set; }
    }
}
