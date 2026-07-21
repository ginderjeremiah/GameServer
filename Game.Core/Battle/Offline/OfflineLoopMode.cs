namespace Game.Core.Battle.Offline
{
    /// <summary>
    /// Which battle the player's idle loop replays for the whole away period. Mirrors the persisted
    /// <see cref="Players.Player.AutoChallengeBoss"/> flag: <see cref="Idle"/> fights random encounters in the
    /// player's current zone, <see cref="Boss"/> auto-challenges that same zone's dedicated boss. Both modes
    /// loop their battle type continuously — wins, losses, and draws all carry on to the next battle (the
    /// online auto-fight-off-on-loss only fires while the player is present to observe it).
    /// </summary>
    public enum OfflineLoopMode
    {
        /// <summary>Idle-farming the current zone: a fresh random encounter (enemy/level/loadout) each battle.</summary>
        Idle,

        /// <summary>Auto-challenging the current zone's dedicated boss: the same boss each battle, with a fresh
        /// seed so the crit/dodge/parry/reflection RNG can vary the outcome.</summary>
        Boss,
    }
}
