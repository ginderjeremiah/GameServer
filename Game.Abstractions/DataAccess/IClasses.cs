using Contracts = Game.Abstractions.Contracts;
using CoreClass = Game.Core.Classes.Class;

namespace Game.Abstractions.DataAccess
{
    public interface IClasses
    {
        /// <summary>The full class catalogue as read contracts (id-as-index order preserved).</summary>
        public List<Contracts.Class> All();

        /// <summary>The lean domain <see cref="CoreClass"/> for <paramref name="classId"/> (a shared,
        /// pre-materialized instance), or null when the id does not resolve.</summary>
        public CoreClass? GetClass(int classId);

        /// <inheritdoc cref="IItems.VersionKey"/>
        public object VersionKey { get; }
    }
}
