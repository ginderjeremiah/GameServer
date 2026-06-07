using Game.Core;
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
    }
}
