using GameCore;
using GameServer.Models.Common;

namespace GameServer.Sockets.Commands
{
    public abstract class AbstractSocketCommand
    {
        public string? Id { get; set; }

        public virtual Task<ApiSocketResponse> ExecuteAsync(SocketContext context)
        {
            var result = Execute(context);
            return Task.FromResult(result);
        }

        public virtual ApiSocketResponse Execute(SocketContext context)
        {
            throw new NotImplementedException();
        }

        public virtual void SetParameters(string? parameters) { }

        public ApiSocketResponse<T> Success<T>(T data)
        {
            return new ApiSocketResponse<T> { Id = Id, Data = data };
        }
        public ApiSocketResponse Error(string errorMessage)
        {
            return new ApiSocketResponse { Id = Id, Error = errorMessage };
        }
        public ApiSocketResponse<T> ErrorWithData<T>(string errorMessage, T data)
        {
            return new ApiSocketResponse<T> { Id = Id, Error = errorMessage, Data = data };
        }
    }

    public abstract class AbstractSocketCommand<TReturn, TParams> : AbstractSocketCommandWithResponseData<TReturn>
    {
        public TParams Parameters { get; set; }

        public override void SetParameters(string? parameters)
        {
            Parameters = parameters.Deserialize<TParams>() ?? throw new ArgumentNullException(nameof(parameters));
        }
    }

    public abstract class AbstractSocketCommandWithParams<T> : AbstractSocketCommand
    {
        public T Parameters { get; set; }

        public override void SetParameters(string? parameters)
        {
            Parameters = parameters.Deserialize<T>() ?? throw new ArgumentNullException(nameof(parameters));
        }
    }

    public abstract class AbstractSocketCommandWithResponseData<T> : AbstractSocketCommand
    {
        public sealed override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context)
        {
            return await HandleExecuteAsync(context);
        }

        public sealed override ApiSocketResponse Execute(SocketContext context)
        {
            return base.Execute(context);
        }

        public virtual Task<ApiSocketResponse<T>> HandleExecuteAsync(SocketContext context)
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
        public string Id { get; set; }
        public string Name { get; set; }
        public string? Parameters { get; set; }

        public SocketCommandInfo() { }

        public override string ToString()
        {
            return $"{{ Id: {Id}, Name: {Name}, Parameters: {Parameters} }}";
        }
    }
}
