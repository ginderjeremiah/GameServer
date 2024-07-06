using GameCore;
using GameServer.Models.Common;

namespace GameServer.Sockets.Commands
{
    public abstract class AbstractSocketCommand
    {
        public abstract Task<ApiSocketResponse> Execute();

        public virtual void SetParameters(string? parameters) { }

        public ApiSocketResponse<T> Success<T>(T data)
        {
            return new ApiSocketResponse<T> { Data = data };
        }
        public ApiSocketResponse Error(string errorMessage)
        {
            return new ApiSocketResponse { Error = errorMessage };
        }
        public ApiSocketResponse<T> ErrorWithData<T>(string errorMessage, T data)
        {
            return new ApiSocketResponse<T> { Error = errorMessage, Data = data };
        }

        public ApiSocketResponse Close(ESocketCloseReason? reason)
        {
            return new ApiSocketResponse
            {
                CloseReason = reason,
            };
        }
    }

    public abstract class AbstractSocketCommand<T> : AbstractSocketCommand
    {
        public T Parameters { get; set; }

        public override void SetParameters(string? parameters)
        {
            Parameters = parameters.Deserialize<T>() ?? throw new ArgumentNullException(nameof(parameters));
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
