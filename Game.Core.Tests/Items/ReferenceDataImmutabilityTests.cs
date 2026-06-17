using System.Reflection;
using System.Runtime.CompilerServices;
using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Enemies;
using Game.Core.Items;
using Game.Core.Progress;
using Game.Core.Skills;
using Game.Core.Zones;
using Xunit;

namespace Game.Core.Tests.Items
{
    /// <summary>
    /// Structural-immutability guard for the shared, cached reference-data domain models (#547) and every
    /// value object reachable through their graphs (e.g. the <c>AttributeModifier</c>/<c>AttributeDistribution</c>
    /// element types held inside their read-only collections, #603). The reference repos hand the same
    /// pre-materialized instance to every player straight from the reference cache, so these types must stay
    /// structurally immutable — every property init-only and every collection an <see cref="IReadOnlyList{T}"/> —
    /// or a stray mutation would silently corrupt the cache for every player. The compiler enforces "you can't
    /// mutate it"; this test enforces "you can't quietly drop the immutability" (flip an <c>init</c> back to
    /// <c>set</c>, or a read-only collection back to a mutable one), which the compiler alone would let pass.
    ///
    /// The covered set is <b>discovered</b> by walking the public-property graph out from the cached aggregate
    /// roots rather than hand-listed (#803): a nested model (or a brand-new field on an existing one) is picked
    /// up automatically, so coverage can't silently lag behind the models the way a maintained list did when
    /// <see cref="AttributeDistribution"/> and <see cref="Challenge"/> were added.
    /// </summary>
    public class ReferenceDataImmutabilityTests
    {
        /// <summary>
        /// The cached reference-data aggregate roots — the gameplay domain models handed out from the
        /// reference caches (items, item mods, skills, enemies, zones, challenges; see
        /// <c>docs/backend.md → Reference Data</c>). Every other covered type is reached by walking these.
        /// <see cref="Enemy"/> is included alongside <see cref="EnemyTemplate"/> because it reuses the
        /// template's shared collections by reference, so it is part of the same shared graph.
        /// </summary>
        private static readonly Type[] ReferenceDataRoots =
        [
            typeof(Item),
            typeof(ItemMod),
            typeof(Skill),
            typeof(EnemyTemplate),
            typeof(Enemy),
            typeof(Zone),
            typeof(Challenge),
        ];

        private static readonly Assembly CoreAssembly = typeof(Item).Assembly;

        public static IEnumerable<object[]> SharedReferenceDataModels()
        {
            return DiscoverReferenceDataModels().Select(type => new object[] { type });
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

        /// <summary>
        /// Sanity check on the discovery itself: the roots that started this guard (#547) must still be
        /// reached, so a refactor that accidentally severs the graph (and thus the coverage) is caught.
        /// </summary>
        [Fact]
        public void Discovery_CoversTheOriginalReferenceDataModels()
        {
            var discovered = DiscoverReferenceDataModels();

            Type[] expected =
            [
                typeof(Item), typeof(ItemMod), typeof(ItemModSlot),
                typeof(Skill), typeof(SkillEffect),
                typeof(AttributeModifier), typeof(AttributeDistribution),
                typeof(Challenge),
            ];

            Assert.All(expected, type => Assert.Contains(type, discovered));
        }

        /// <summary>
        /// Breadth-first walk of the public-property graph from the <see cref="ReferenceDataRoots"/>,
        /// collecting every reachable type defined in the <c>Game.Core</c> assembly. Collection/nullable
        /// generic arguments are unwrapped so the element type of a read-only collection is covered, and a
        /// visited set keeps the walk terminating on shared/back-referencing nodes.
        /// </summary>
        private static HashSet<Type> DiscoverReferenceDataModels()
        {
            var discovered = new HashSet<Type>();
            var queue = new Queue<Type>(ReferenceDataRoots);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (!IsCoreModel(current) || !discovered.Add(current))
                {
                    continue;
                }

                foreach (var property in PublicInstanceProperties(current))
                {
                    foreach (var candidate in ModelTypesIn(property.PropertyType))
                    {
                        queue.Enqueue(candidate);
                    }
                }
            }

            return discovered;
        }

        /// <summary>
        /// The model types carried by a property type: the type itself, or — for a constructed generic such as
        /// <see cref="IReadOnlyList{T}"/> or <see cref="Nullable{T}"/> — its generic arguments, recursively.
        /// </summary>
        private static IEnumerable<Type> ModelTypesIn(Type propertyType)
        {
            if (propertyType.IsGenericType)
            {
                return propertyType.GetGenericArguments().SelectMany(ModelTypesIn);
            }

            return [propertyType];
        }

        /// <summary>A type counts as a reference-data model to walk/cover when it is a non-enum class or struct
        /// authored in the <c>Game.Core</c> assembly (skipping primitives, strings, enums and framework types).</summary>
        private static bool IsCoreModel(Type type)
        {
            return type.Assembly == CoreAssembly && !type.IsEnum && (type.IsClass || type.IsValueType) && type != typeof(string);
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
