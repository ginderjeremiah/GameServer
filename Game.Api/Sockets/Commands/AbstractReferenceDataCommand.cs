using Game.Api.Models.Common;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Base for socket commands that fetch a read-only reference-data collection
    /// (the static/intrinsic data the loading screen needs). Mirrors the
    /// corresponding reference-data HTTP endpoints so the data can be loaded over
    /// the authenticated WebSocket connection instead of a separate HTTP request.
    /// </summary>
    /// <typeparam name="TModel">The API model type returned in the collection.</typeparam>
    public abstract class AbstractReferenceDataCommand<TModel> : AbstractSocketCommandWithResponseData<IEnumerable<TModel>>
    {
        public override ApiSocketResponse<IEnumerable<TModel>> HandleExecute(SocketContext context)
        {
            return Success(GetReferenceData());
        }

        protected abstract IEnumerable<TModel> GetReferenceData();
    }
}
