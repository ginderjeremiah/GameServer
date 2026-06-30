using Game.Abstractions.Contracts;
using Game.Api.Sockets.Commands;
using Game.Core;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Pins that an item's weapon type is part of the client-visible serialization, so setting it changes the
    /// items reference-data version hash and clients re-download the set once (the expected one-time effect of
    /// introducing the field).
    /// </summary>
    public class ItemWeaponTypeVersioningTests
    {
        [Fact]
        public void ComputeVersion_ChangesWhenWeaponTypeChanges()
        {
            var baseline = ReferenceDataVersioning.ComputeVersion(new[] { NewItem(weaponType: null) });
            var typed = ReferenceDataVersioning.ComputeVersion(new[] { NewItem(weaponType: EDamageType.Sword) });

            Assert.NotEqual(baseline, typed);
        }

        private static Item NewItem(EDamageType? weaponType) => new()
        {
            Id = 0,
            Name = "Test",
            Description = "",
            IconPath = "",
            WeaponType = weaponType,
            Attributes = [],
            ModSlots = [],
            Tags = [],
        };
    }
}
