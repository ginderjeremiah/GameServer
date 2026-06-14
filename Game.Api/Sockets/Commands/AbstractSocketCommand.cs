using Game.Api.Models.Common;
using Game.Core;

namespace Game.Api.Sockets.Commands
{
    public abstract class AbstractSocketCommand
    {
        public string? Id { get; set; }
        public abstract string Name { get; set; }

        public virtual Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var result = Execute(context);
            return Task.FromResult(result);
        }

        public virtual ApiSocketResponse Execute(SocketContext context)
        {
            throw new NotImplementedException();
        }

        public virtual void SetParameters(string? parameters) { }

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
            Parameters = parameters.Deserialize<TParams>() ?? throw new ArgumentNullException(nameof(parameters));
        }
    }

    public abstract class AbstractSocketCommandWithParams<T> : AbstractSocketCommand
    {
        public required T Parameters { get; set; }

        public override void SetParameters(string? parameters)
        {
            Parameters = parameters.Deserialize<T>() ?? throw new ArgumentNullException(nameof(parameters));
        }
    }

    public abstract class AbstractSocketCommandWithResponseData<T> : AbstractSocketCommand
    {
        public sealed override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            return await HandleExecuteAsync(context, cancellationToken);
        }

        public sealed override ApiSocketResponse Execute(SocketContext context)
        {
            return base.Execute(context);
        }

        public virtual Task<ApiSocketResponse<T>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var result = HandleExecute(context);
            return Task.FromResult(result);
        }

        public virtual ApiSocketResponse<T> HandleExecute(SocketContext context)
        {
            throw new NotImplementedException();
        }
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
}
