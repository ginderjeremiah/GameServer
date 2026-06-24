using Game.Core;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Computes the content version (hash) the frontend uses to decide whether its
    /// locally-cached copy of a reference-data set is still current.
    /// </summary>
    internal static class ReferenceDataVersioning
    {
        // Memoizes each set's computed hash against the immutable snapshot instance it was computed from, so
        // the expensive serialize-and-hash runs once per cache swap rather than once per connect (the version
        // is the first thing the loading screen pulls). Keying on the snapshot reference makes invalidation
        // automatic: a build-then-swap publishes a new snapshot object, which is a cache miss, while the old
        // entry is collected once no caller holds the prior snapshot. ConditionalWeakTable is itself
        // thread-safe; its GetValue may run the factory more than once under a concurrent first-touch of the
        // same key (only one result is published and returned), which is harmless here because the hash is a
        // pure, idempotent function of the snapshot — a redundant recompute yields the identical value.
        private static readonly ConditionalWeakTable<object, string> _versions = new();

        /// <summary>
        /// Hashes a reference-data set's serialized models. The set is serialized through its
        /// concrete <typeparamref name="TModel"/> (not <c>object</c>), so every model property is
        /// included and the hash is deterministic for a given set/order. The result therefore
        /// changes only when the client-visible data changes.
        /// </summary>
        public static string ComputeVersion<TModel>(IEnumerable<TModel> models)
        {
            var json = models.Serialize();
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
            return Convert.ToHexString(hash);
        }

        /// <summary>
        /// Returns the cached version for <paramref name="snapshotKey"/>, computing it from
        /// <paramref name="models"/> only on the first request for that snapshot. <paramref name="snapshotKey"/>
        /// must be the immutable snapshot instance the data is drawn from: its reference identity changes on a
        /// cache swap, which is exactly when the version must change, so a swap naturally invalidates the entry.
        /// <paramref name="models"/> is evaluated lazily so the serialization is skipped entirely on a cache hit.
        /// </summary>
        public static string GetOrComputeVersion<TModel>(object snapshotKey, Func<IEnumerable<TModel>> models)
        {
            return _versions.GetValue(snapshotKey, _ => ComputeVersion(models()));
        }
    }
}
