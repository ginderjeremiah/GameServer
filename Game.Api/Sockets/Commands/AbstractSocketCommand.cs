using Game.Api.Models.Common;
using Game.Core;
using System.Text.Json;

namespace Game.Api.Sockets.Commands
{
    public abstract class AbstractSocketCommand
    {
        public string? Id { get; set; }
        public abstract string Name { get; set; }

        // The single execution entry point. Abstract (rather than a virtual with a sync companion that
        // throws) so the compiler forces every command to implement exactly this one method — there is no
        // wrong-override-at-runtime path. A synchronous handler returns Task.FromResult(...).
        public abstract Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken);

        public virtual void SetParameters(string? parameters) { }

        /// <summary>
        /// Shared by the two parameter-bearing base classes below: deserializes <paramref name="parameters"/>,
        /// wrapping a malformed/missing-JSON failure as <see cref="MalformedSocketCommandParametersException"/>
        /// right where the malformed-params semantics are actually known — a bad request, not a server fault —
        /// rather than a caller further up guessing from the exception's type (#1498).
        /// </summary>
        protected TParams DeserializeParameters<TParams>(string? parameters)
        {
            try
            {
                return parameters.Deserialize<TParams>() ?? throw new ArgumentNullException(nameof(parameters));
            }
            catch (Exception ex) when (ex is JsonException or ArgumentNullException)
            {
                throw new MalformedSocketCommandParametersException(Name, ex);
            }
        }

        public ApiSocketResponse Success()
        {
            return new ApiSocketResponse { Id = Id, Name = Name };
        }

        public ApiSocketResponse<T> Success<T>(T data)
        {
            return new ApiSocketResponse<T> { Id = Id, Name = Name, Data = data };
        }

        public ApiSocketResponse Error(string errorMessage)
        {
            return new ApiSocketResponse { Id = Id, Name = Name, Error = errorMessage };
        }

        public ApiSocketResponse<T> ErrorWithData<T>(string errorMessage, T data)
        {
            return new ApiSocketResponse<T> { Id = Id, Name = Name, Error = errorMessage, Data = data };
        }
    }

    public abstract class AbstractSocketCommand<TReturn, TParams> : AbstractSocketCommandWithResponseData<TReturn>
    {
        public required TParams Parameters { get; set; }

        public override void SetParameters(string? parameters)
        {
            Parameters = DeserializeParameters<TParams>(parameters);
        }
    }

    public abstract class AbstractSocketCommandWithParams<T> : AbstractSocketCommand
    {
        public required T Parameters { get; set; }

        public override void SetParameters(string? parameters)
        {
            Parameters = DeserializeParameters<T>(parameters);
        }
    }

    public abstract class AbstractSocketCommandWithResponseData<T> : AbstractSocketCommand
    {
        // Seals the base entry point onto the typed handler so commands implement only HandleExecuteAsync,
        // whose ApiSocketResponse<T> return is what the codegen reads to type the client response.
        public sealed override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            return await HandleExecuteAsync(context, cancellationToken);
        }

        public abstract Task<ApiSocketResponse<T>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken);
    }

    public class SocketCommandInfo
    {
        public string? Id { get; set; }
        public string Name { get; set; }
        public string? Parameters { get; set; }

        public SocketCommandInfo(string name)
        {
            Name = name;
        }

        public override string ToString()
        {
            return $"{{ Id: {Id}, Name: {Name}, Parameters: {Parameters} }}";
        }
    }

    /// <summary>
    /// Wraps a command dead-lettered onto <see cref="Constants.PUBSUB_SOCKET_DEAD_LETTER_QUEUE"/> with the
    /// player id it was addressed to (#1542). A bare <see cref="SocketCommandInfo"/> carries no addressing
    /// info once dequeued, and the specific socket it originally failed on may no longer even exist by the
    /// time an operator replays it — so <see cref="PlayerId"/>, not the socket id, is what a replay targets
    /// (<c>SocketManagerService.EmitSocketCommand(SocketCommandInfo, int)</c> resolves whatever socket is
    /// currently live for that player).
    /// </summary>
    internal class SocketCommandDeadLetterEnvelope
    {
        public required int PlayerId { get; set; }
        public required SocketCommandInfo Command { get; set; }
    }
}
