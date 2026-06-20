using Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories
{
    /// <summary>
    /// Index helpers for the zero-based-id reference snapshots (items, skills, item mods, enemies, zones,
    /// challenges). A static reference record's Id is its index into the cached list — contiguity is
    /// guaranteed structurally by retire-not-delete (see docs/backend.md → Reference Data) — so resolving one
    /// by id is an array index. Centralizing the bounds check keeps the <c>Lookup*</c> (null) and <c>Get*</c>
    /// (throw) shapes symmetric and makes a bad id (a stale player row, migration drift) fail meaningfully
    /// rather than throwing a bare <see cref="ArgumentOutOfRangeException"/> from the raw indexer.
    /// </summary>
    internal static class ReferenceSnapshotExtensions
    {
        /// <summary>
        /// Returns the record whose Id (index) is <paramref name="id"/>, or <c>null</c> when the id is out of
        /// range. This is the <c>Lookup*</c> shape, for callers that handle a missing record themselves.
        /// </summary>
        public static T? Lookup<T>(this IReadOnlyList<T> snapshot, int id) where T : class
        {
            return id < 0 || snapshot.Count <= id ? null : snapshot[id];
        }

        /// <summary>
        /// Returns the record whose Id (index) is <paramref name="id"/>, throwing a descriptive
        /// <see cref="ArgumentOutOfRangeException"/> naming the id and <paramref name="setName"/> when the id is
        /// out of range. This is the gameplay <c>Get*</c> shape, where a non-resolving id is a corrupt
        /// reference that should surface meaningfully rather than as an opaque indexer crash.
        /// </summary>
        public static T GetById<T>(this IReadOnlyList<T> snapshot, int id, string setName)
        {
            if (id < 0 || snapshot.Count <= id)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, $"No {setName} exists with Id {id}.");
            }

            return snapshot[id];
        }

        /// <summary>
        /// Asserts the zero-based-id contiguity invariant the snapshots rely on: the record at index
        /// <c>i</c> must have <c>Id == i</c>, for every <c>i</c>. <see cref="OrderBy{T}"/>-by-id guarantees
        /// sort order but not that the ids run 0..n-1 with no gaps; a seed/migration gap (the one way a gap
        /// could appear, since hard-delete is blocked) would make every index lookup above the gap silently
        /// resolve the wrong record. Throwing here fails the build-then-swap so the prior good snapshot stays
        /// in place (and a startup load surfaces the corruption as a boot failure) rather than serving
        /// silently mis-resolved data.
        /// </summary>
        public static void AssertZeroBasedContiguity<T>(this IReadOnlyList<T> snapshot, Func<T, int> idSelector, string setName)
        {
            for (var i = 0; i < snapshot.Count; i++)
            {
                var id = idSelector(snapshot[i]);
                if (id != i)
                {
                    throw new InvalidOperationException(
                        $"Reference set '{setName}' violates the zero-based-id contiguity invariant: the record at " +
                        $"index {i} has Id {id} (expected {i}). Ids must be seeded from 0 and contiguous; a gap " +
                        "indicates a seed/migration mistake (top-level reference records are retired, never deleted).");
                }
            }
        }

        /// <summary>
        /// Entity overload of <see cref="AssertZeroBasedContiguity{T}(IReadOnlyList{T}, Func{T, int}, string)"/>
        /// for the cached entity lists, reading <c>Id</c> off the shared
        /// <see cref="IZeroBasedIdentityEntity"/> contract so no per-set id selector is needed.
        /// </summary>
        public static void AssertZeroBasedContiguity(this IReadOnlyList<IZeroBasedIdentityEntity> snapshot, string setName)
        {
            snapshot.AssertZeroBasedContiguity(entity => entity.Id, setName);
        }
    }
}
