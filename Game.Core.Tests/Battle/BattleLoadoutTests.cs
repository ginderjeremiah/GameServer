using Game.Core.Battle;
using Xunit;

namespace Game.Core.Tests.Battle
{
    public class BattleLoadoutTests
    {
        // ── IsFielded (the weapon-match gate predicate) ──────────────────────

        [Theory]
        // Weapon-leaf skill: fielded only when it matches the equipped weapon type (exact leaf-match).
        [InlineData(EDamageType.Sword, EDamageType.Sword, true)]
        [InlineData(EDamageType.Sword, EDamageType.Axe, false)]
        [InlineData(EDamageType.Sword, EDamageType.Unarmed, false)]
        [InlineData(EDamageType.Unarmed, EDamageType.Unarmed, true)]
        [InlineData(EDamageType.Unarmed, EDamageType.Sword, false)]
        [InlineData(EDamageType.Dagger, EDamageType.Dagger, true)]
        public void IsFielded_WeaponLeafSkill_FieldedOnlyOnExactMatch(
            EDamageType skillType, EDamageType weaponType, bool expected)
        {
            Assert.Equal(expected, BattleLoadout.IsFielded(skillType, weaponType));
        }

        [Theory]
        // Weapon-agnostic types are never gated — fielded regardless of the equipped weapon.
        [InlineData(EDamageType.Physical)]
        [InlineData(EDamageType.Fire)]
        [InlineData(EDamageType.Water)]
        [InlineData(EDamageType.Bleed)]
        [InlineData(EDamageType.Poison)]
        [InlineData(EDamageType.Burn)]
        public void IsFielded_AgnosticSkill_AlwaysFielded(EDamageType agnosticType)
        {
            foreach (var weaponType in new[] { EDamageType.Unarmed, EDamageType.Sword, EDamageType.Bow })
            {
                Assert.True(BattleLoadout.IsFielded(agnosticType, weaponType));
            }
        }

        // ── OrderSkillIds (order + dedupe + gate) ────────────────────────────

        [Fact]
        public void OrderSkillIds_SelectedThenGranted_DeDupedFirstWins()
        {
            // 1 selected, 2 selected, 1 granted (dup of selected), 3 granted — all Physical (agnostic).
            var types = Types((1, EDamageType.Physical), (2, EDamageType.Physical), (3, EDamageType.Physical));

            var fielded = BattleLoadout
                .OrderSkillIds([1, 2], [1, 3], EDamageType.Unarmed, id => types[id]).ToList();

            Assert.Equal([1, 2, 3], fielded);
        }

        [Fact]
        public void OrderSkillIds_WeaponLeafSelectedSkill_DroppedWhenWeaponMismatches()
        {
            // A Sword-typed selected skill is dormant bare-handed (Unarmed); the agnostic one stays.
            var types = Types((1, EDamageType.Sword), (2, EDamageType.Physical));

            var fielded = BattleLoadout
                .OrderSkillIds([1, 2], [], EDamageType.Unarmed, id => types[id]).ToList();

            Assert.Equal([2], fielded);
        }

        [Fact]
        public void OrderSkillIds_WeaponLeafSelectedSkill_FieldedWhenWeaponMatches()
        {
            var types = Types((1, EDamageType.Sword), (2, EDamageType.Physical));

            var fielded = BattleLoadout
                .OrderSkillIds([1, 2], [], EDamageType.Sword, id => types[id]).ToList();

            Assert.Equal([1, 2], fielded);
        }

        [Fact]
        public void OrderSkillIds_GateAppliesUniformlyToGrantedSkills()
        {
            // A non-weapon item that granted an Axe skill is dormant unless an Axe is wielded — here a Sword is.
            var types = Types((1, EDamageType.Physical), (2, EDamageType.Axe));

            var fielded = BattleLoadout
                .OrderSkillIds([1], [2], EDamageType.Sword, id => types[id]).ToList();

            Assert.Equal([1], fielded);
        }

        [Fact]
        public void OrderSkillIds_UnresolvableId_IsDropped()
        {
            // An id with no resolvable skill (a null type — e.g. an unauthored punch) is dropped, not fielded.
            var types = Types((1, EDamageType.Physical));

            var fielded = BattleLoadout
                .OrderSkillIds([1], [99], EDamageType.Unarmed, id => types.TryGetValue(id, out var t) ? t : null).ToList();

            Assert.Equal([1], fielded);
        }

        private static Dictionary<int, EDamageType?> Types(params (int Id, EDamageType Type)[] entries)
        {
            return entries.ToDictionary(e => e.Id, e => (EDamageType?)e.Type);
        }
    }
}
