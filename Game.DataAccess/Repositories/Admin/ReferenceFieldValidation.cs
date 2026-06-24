using Game.Abstractions;
using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Shared up-front guards for authored intrinsic-enum reference fields (e.g. rarity, category, mod type).
    /// Each such field is persisted as a foreign key into an enum-seeded reference table, so a value outside
    /// its <see cref="Enum"/> (a missing/<c>0</c> payload, or any unmapped tier) has no backing row and would
    /// otherwise surface as an opaque <c>FK</c> 500 at commit instead of a clean validation rejection. Running
    /// the check here — before anything is staged — keeps the rejection a graceful <see cref="AdminSaveResult"/>
    /// (mapped to a 400) and leaves the batch unmodified.
    /// </summary>
    /// <remarks>
    /// This deliberately covers only single-value enum FKs. A <c>[Flags]</c> field (e.g. a skill's acquisition
    /// bitmask) is a free combination of its members, so <see cref="Enum.IsDefined{TEnum}(TEnum)"/> would wrongly
    /// reject valid composite values — those need no such guard.
    /// </remarks>
    internal static class ReferenceFieldValidation
    {
        /// <summary>
        /// Returns a rejection for the first added/edited change whose <paramref name="field"/> value is not a
        /// defined <typeparamref name="TEnum"/> member, or <c>null</c> when every change carries a valid value.
        /// Deletes are skipped — a delete targets an existing row by id and carries no authored field worth validating.
        /// </summary>
        public static AdminSaveResult? FindUndefinedEnum<T, TEnum>(
            IReadOnlyList<Change<T>> changes,
            Func<T, TEnum> field,
            string fieldName)
            where T : IModel
            where TEnum : struct, Enum
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == EChangeType.Delete)
                {
                    continue;
                }

                var value = field(change.Item);
                if (!Enum.IsDefined(value))
                {
                    return AdminSaveResult.Failure(
                        $"{Convert.ToInt32(value)} is not a valid {fieldName}.");
                }
            }

            return null;
        }
    }
}
