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
    public abstract class AbstractReferenceDataCommand<TModel> : AbstractSocketCommandWithResponseData<IEnumerable<TModel>>, IReferenceDataCommand
    {
        public override Task<ApiSocketResponse<IEnumerable<TModel>>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(Success(GetReferenceData()));
        }

        /// <summary>
        /// Hashes this set's current data so the frontend can detect a stale cache. Computed from
        /// the same models <see cref="HandleExecuteAsync"/> returns, so the version tracks exactly what
        /// a client would download.
        /// </summary>
        public string ComputeVersion()
        {
            return ReferenceDataVersioning.ComputeVersion(GetReferenceData());
        }

        protected abstract IEnumerable<TModel> GetReferenceData();
    }
}
