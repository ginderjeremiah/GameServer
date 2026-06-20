using Game.Core.Battle;
using Game.Core.Players;

namespace Game.Core.TestInfrastructure.Builders
{
    /// <summary>
    /// Test-only convenience for building a <see cref="Battler"/> straight from a live <see cref="Player"/>
    /// aggregate. Production never does this: it reconstructs a player's battler from a frozen
    /// <see cref="BattleSnapshot"/> (the anti-cheat replay surface). This shortcut keeps battle tests terse
    /// by composing the same attributes/skills/level off the live aggregate, and the two construction paths
    /// are pinned to agree by BattleSnapshotTests.AssertBattlerParity.
    /// </summary>
    public static class BattlerFactory
    {
        public static Battler FromPlayer(Player player) =>
            new(player.GetAttributes(), player.SelectedSkills, player.Level);
    }
}
