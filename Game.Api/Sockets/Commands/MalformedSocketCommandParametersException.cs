namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Thrown when a socket command's <c>Parameters</c> cannot be bound — malformed JSON or a missing value
    /// where one is required. Distinct from a genuine command fault so <see cref="SocketHandler"/> can reject
    /// it as a bad request ("Malformed parameters.") rather than misclassifying a client input error as an
    /// "Internal Server Error".
    /// </summary>
    public class MalformedSocketCommandParametersException : Exception
    {
        public MalformedSocketCommandParametersException(string commandName, Exception innerException)
            : base($"Failed to bind parameters for socket command '{commandName}'.", innerException) { }
    }
}
