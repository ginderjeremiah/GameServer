using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Core.Attributes.Modifiers;
using Game.Core.Items;
using Game.Core.Skills;
using Xunit;

namespace Game.Core.Tests.Items
{
    /// <summary>
    /// Structural-immutability guard for the shared, cached reference-data domain models (#547) and the
    /// <see cref="AttributeModifier"/> element type held inside their read-only collections (#603).
    /// <c>GetItem</c>/<c>GetItemMod</c>/<c>GetSkill</c> hand the same pre-materialized instance to every
    /// player straight from the reference cache, so these types must stay structurally immutable — every
    /// property init-only and every collection an <see cref="IReadOnlyList{T}"/> — or a stray mutation would
    /// silently corrupt the cache for every player. The compiler enforces "you can't mutate it"; this test
    /// enforces "you can't quietly drop the immutability" (flip an <c>init</c> back to <c>set</c>, or a
    /// read-only collection back to a mutable one), which the compiler alone would let pass.
    /// </summary>
    public class ReferenceDataImmutabilityTests
    {
        public static IEnumerable<object[]> SharedReferenceDataModels()
        {
            yield return new object[] { typeof(Item) };
            yield return new object[] { typeof(ItemMod) };
            yield return new object[] { typeof(ItemModSlot) };
            yield return new object[] { typeof(Skill) };
            yield return new object[] { typeof(SkillEffect) };
            yield return new object[] { typeof(AttributeModifier) };
        }

        [Theory]
        [MemberData(nameof(SharedReferenceDataModels))]
        public void PublicProperties_AreInitOnly(Type modelType)
        {
            var mutable = PublicInstanceProperties(modelType)
                .Where(property => property.SetMethod is { IsPublic: true } setter && !IsInitOnly(setter))
                .Select(property => property.Name)
                .ToList();

            Assert.True(mutable.Count == 0,
                $"{modelType.Name} exposes mutable (non-init) public setters: {string.Join(", ", mutable)}");
        }

        [Theory]
        [MemberData(nameof(SharedReferenceDataModels))]
        public void CollectionProperties_AreReadOnly(Type modelType)
        {
            var mutableCollections = PublicInstanceProperties(modelType)
                .Where(property => IsMutableCollection(property.PropertyType))
                .Select(property => property.Name)
                .ToList();

            Assert.True(mutableCollections.Count == 0,
                $"{modelType.Name} exposes mutable collection properties: {string.Join(", ", mutableCollections)}");
        }

        private static IEnumerable<PropertyInfo> PublicInstanceProperties(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        }

        /// <summary>An init-only setter carries the <see cref="IsExternalInit"/> required modifier.</summary>
        private static bool IsInitOnly(MethodInfo setter)
        {
            return setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit));
        }

        /// <summary>
        /// A type is a mutable collection if it (or one of its interfaces) is a closed <see cref="ICollection{T}"/> —
        /// which <see cref="List{T}"/>, <see cref="IList{T}"/>, arrays, etc. all satisfy, while
        /// <see cref="IReadOnlyList{T}"/> does not.
        /// </summary>
        private static bool IsMutableCollection(Type type)
        {
            return type.GetInterfaces().Append(type)
                .Any(candidate => candidate.IsGenericType
                    && candidate.GetGenericTypeDefinition() == typeof(ICollection<>));
        }
    }
}
