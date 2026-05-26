namespace Game.Core.Battle
{
    public record BattleResult(bool Victory, bool PlayerDied, int TotalMs, BattleStats Stats);
}
