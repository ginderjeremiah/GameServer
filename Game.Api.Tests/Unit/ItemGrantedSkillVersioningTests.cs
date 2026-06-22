using Game.Abstractions.Contracts;
using Game.Api.Sockets.Commands;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Pins that an item's granted skill is part of the client-visible serialization, so setting it changes
    /// the items reference-data version hash and clients re-download the set once (the expected one-time
    /// effect of introducing the field).
    /// </summary>
    public class ItemGrantedSkillVersioningTests
    {
        [Fact]
        public void ComputeVersion_ChangesWhenGrantedSkillChanges()
        {
            var baseline = ReferenceDataVersioning.ComputeVersion(new[] { NewItem(grantedSkillId: null) });
            var granted = ReferenceDataVersioning.ComputeVersion(new[] { NewItem(grantedSkillId: 3) });

            Assert.NotEqual(baseline, granted);
        }

        private static Item NewItem(int? grantedSkillId) => new()
        {
            Id = 0,
            Name = "Test",
            Description = "",
            IconPath = "",
            GrantedSkillId = grantedSkillId,
            Attributes = [],
            ModSlots = [],
            Tags = [],
        };
    }
}
