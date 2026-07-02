using Game.Abstractions.Contracts;
using Game.Api.Models.Common;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Base for socket commands that fetch a read-only reference-data collection
    /// (the static/intrinsic data the loading screen needs), served over the
    /// authenticated WebSocket connection.
    /// </summary>
    /// <typeparam name="TModel">The API model type returned in the collection.</typeparam>
    public abstract class AbstractReferenceDataCommand<TModel> : AbstractSocketCommandWithResponseData<IEnumerable<TModel>>, IReferenceDataCommand
    {
        // GetReferenceData projects the whole set fresh (e.g. the repo re-maps its cached entities to
        // contracts). That is a cold path, not a per-connect cost: the loading screen issues
        // GetReferenceDataVersions first and only fetches a set whose version it doesn't already have cached,
        // so a full read happens on a content change (or a first/cleared cache), not on every connect. The
        // per-connect cost is the version hash, which is already memoized per snapshot (ComputeVersion) — so
        // projecting the set once per snapshot here would optimize a path that isn't hot.
        public override Task<ApiSocketResponse<IEnumerable<TModel>>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(Success(RedactAuthoringOnlyFields(GetReferenceData(), context.IsAdmin)));
        }

        /// <summary>
        /// Blanks <see cref="IHasDesignerNotes.DesignerNotes"/> for a non-admin connection — the Workbench
        /// (an admin-role connection) still authors it over this same command, so an admin gets it unredacted.
        /// <see cref="ComputeVersion"/> hashes a separate, unredacted <see cref="GetReferenceData"/> call, so
        /// authoring a note still bumps the set's version hash once for every client, admin or not — only the
        /// wire payload differs. Each set's mapper returns fresh model instances per call (never a shared
        /// cached instance), so mutating them here is safe.
        /// </summary>
        private static IEnumerable<TModel> RedactAuthoringOnlyFields(IEnumerable<TModel> models, bool isAdmin)
        {
            if (isAdmin)
            {
                return models;
            }

            return models.Select(model =>
            {
                if (model is IHasDesignerNotes hasDesignerNotes)
                {
                    hasDesignerNotes.DesignerNotes = "";
                }
                return model;
            });
        }

        /// <summary>
        /// Hashes this set's current, genuine data (via <see cref="GetReferenceData"/> directly, deliberately
        /// bypassing <see cref="HandleExecuteAsync"/>'s admin-only redaction) so the version tracks the
        /// underlying content regardless of who's asking — an authoring edit bumps it once for every client.
        /// The hash is memoized against <see cref="VersionKey"/> so it is computed once per cache swap rather
        /// than re-serialized on every connect (this is the first command the loading screen issues, for
        /// every player).
        /// </summary>
        public string ComputeVersion()
        {
            return ReferenceDataVersioning.GetOrComputeVersion(VersionKey, GetReferenceData);
        }

        protected abstract IEnumerable<TModel> GetReferenceData();

        /// <summary>
        /// The immutable instance the memoized version is keyed on; its reference identity must change exactly
        /// when this set's client-visible data changes. For a database-backed set this is the cache holder's
        /// current snapshot (a build-then-swap publishes a new instance), so a swap invalidates the cached
        /// version automatically. For an intrinsic (enum-derived) set the data is fixed for the process
        /// lifetime, so a stable per-set sentinel is the natural key — see <see cref="IntrinsicVersionKey"/>.
        /// </summary>
        protected abstract object VersionKey { get; }
    }
}
