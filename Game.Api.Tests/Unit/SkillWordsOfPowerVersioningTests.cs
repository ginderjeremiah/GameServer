using Game.Abstractions.Contracts;
using Game.Api.Sockets.Commands;
using Game.Core;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Pins that a skill's word of power (and its pronunciation/translation) is part of the client-visible
    /// serialization, so authoring it changes the skills reference-data version hash and clients re-download
    /// the set once (the expected one-time effect of introducing the field).
    /// </summary>
    public class SkillWordsOfPowerVersioningTests
    {
        [Fact]
        public void ComputeVersion_ChangesWhenWordChanges()
        {
            var baseline = ReferenceDataVersioning.ComputeVersion(new[] { NewSkill(word: "") });
            var named = ReferenceDataVersioning.ComputeVersion(new[] { NewSkill(word: "sijren") });

            Assert.NotEqual(baseline, named);
        }

        private static Skill NewSkill(string word) => new()
        {
            Id = 0,
            Name = "Test",
            BaseDamage = 1m,
            DamageMultipliers = [],
            Effects = [],
            Description = "",
            IconPath = "",
            RarityId = ERarity.Common,
            Word = word,
            Pronunciation = "",
            Translation = "",
            Acquisition = ESkillAcquisition.Player,
        };
    }
}
