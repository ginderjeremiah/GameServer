using Game.Core;
using Game.Core.Battle;
using Game.Core.Enemies;
using Game.Core.Players;
using Game.Core.Progress;
using Game.Core.TestInfrastructure.Builders;
using Xunit;

namespace Game.Core.Tests.Progress
{
    public class PlayerProgressTests
    {
        private static readonly DateTime Timestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // ── RecordBattleCompleted: global statistics ─────────────────────────

        [Fact]
        public void RecordBattleCompleted_TracksDamageStatistics()
        {
            var progress = MakeProgress();
            var stats = new BattleStats
            {
                PlayerDamageDealt = 120.5,
                PlayerDamageTaken = 40.0,
                PlayerDamageHealed = 18.75,
                HighestPlayerAttack = 33.25,
            };

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 4000, stats,
                isBossBattle: false, zoneId: 0);

            Assert.Equal(120.5m, progress.GetStatisticValue(EStatisticType.DamageDealt, null));
            Assert.Equal(40.0m, progress.GetStatisticValue(EStatisticType.DamageTaken, null));
            Assert.Equal(18.75m, progress.GetStatisticValue(EStatisticType.DamageHealed, null));
            Assert.Equal(33.25m, progress.GetStatisticValue(EStatisticType.HighestSingleAttackDamage, null));
        }

        [Fact]
        public void RecordBattleCompleted_TracksCritDodgeStatistics()
        {
            var progress = MakeProgress();
            var stats = new BattleStats
            {
                CriticalHits = 3,
                CriticalDamageDealt = 88.5,
                // The Precision training signal is a separate field (#1448); it must not leak into the
                // player-facing CriticalDamageDealt statistic, which stays the actual crit damage dealt.
                CriticalBonusDealt = 42.0,
                AttacksDodged = 2,
                DamageDodged = 14.25,
            };

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 4000, stats,
                isBossBattle: false, zoneId: 0);

            Assert.Equal(3m, progress.GetStatisticValue(EStatisticType.CriticalHits, null));
            Assert.Equal(88.5m, progress.GetStatisticValue(EStatisticType.CriticalDamageDealt, null));
            Assert.Equal(2m, progress.GetStatisticValue(EStatisticType.AttacksDodged, null));
            Assert.Equal(14.25m, progress.GetStatisticValue(EStatisticType.DamageDodged, null));
        }

        [Fact]
        public void RecordBattleCompleted_NegativeNetUnderAbsorption_FloorsDamageStatisticsAtZeroInsteadOfRecordingARow()
        {
            // An enemy with authored absorption (resistance > 1) can heal the player back more than the
            // battle's real hits dealt/took, so the in-battle (signed, parity-critical) totals go negative.
            // The lifetime Sum statistics must never regress from that — a floored-to-zero delta carries no
            // information, so (like any other zero delta) it records no row at all (#2127).
            var progress = MakeProgress();
            var stats = new BattleStats
            {
                PlayerDamageDealt = -12.0,
                PlayerDamageTaken = -3.0,
                CriticalDamageDealt = -6.5,
            };

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000, stats,
                isBossBattle: false, zoneId: 0);

            Assert.False(progress.TryGetStatisticValue(EStatisticType.DamageDealt, null, out _));
            Assert.False(progress.TryGetStatisticValue(EStatisticType.DamageTaken, null, out _));
            Assert.False(progress.TryGetStatisticValue(EStatisticType.CriticalDamageDealt, null, out _));
        }

        [Fact]
        public void RecordBattleCompleted_NegativeNetUnderAbsorption_NeverRegressesAnExistingLifetimeTotal()
        {
            var progress = MakeProgress(statistics:
            [
                Stat(EStatisticType.DamageDealt, null, 100m),
                Stat(EStatisticType.DamageTaken, null, 50m),
                Stat(EStatisticType.CriticalDamageDealt, null, 20m),
            ]);
            var stats = new BattleStats
            {
                PlayerDamageDealt = -8.0,
                PlayerDamageTaken = -4.0,
                CriticalDamageDealt = -2.0,
            };

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000, stats,
                isBossBattle: false, zoneId: 0);

            Assert.Equal(100m, progress.GetStatisticValue(EStatisticType.DamageDealt, null));
            Assert.Equal(50m, progress.GetStatisticValue(EStatisticType.DamageTaken, null));
            Assert.Equal(20m, progress.GetStatisticValue(EStatisticType.CriticalDamageDealt, null));
            Assert.DoesNotContain(progress.DirtyStatistics, s => s.Type == EStatisticType.DamageDealt && s.EntityId == null);
            Assert.DoesNotContain(progress.DirtyStatistics, s => s.Type == EStatisticType.DamageTaken && s.EntityId == null);
            Assert.DoesNotContain(progress.DirtyStatistics, s => s.Type == EStatisticType.CriticalDamageDealt && s.EntityId == null);
        }

        [Fact]
        public void RecordBattleCompleted_PerSkillNegativeNetUnderAbsorption_FloorsAtZeroInsteadOfRecordingARow()
        {
            var progress = MakeProgress();
            var stats = new BattleStats
            {
                SkillStats = { [10] = new SkillStats { Uses = 1, TotalDamage = -9.0 } },
            };

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000, stats,
                isBossBattle: false, zoneId: 0);

            Assert.False(progress.TryGetStatisticValue(EStatisticType.DamageDealt, 10, out _));
        }

        [Fact]
        public void RecordBattleCompleted_TracksEncountersGloballyAndPerEnemy()
        {
            var progress = MakeProgress();
            var enemy = MakeEnemy(id: 7);

            progress.RecordBattleCompleted(enemy, victory: true, playerDied: false, totalMs: 1000, new BattleStats(),
                isBossBattle: false, zoneId: 0);

            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.EnemiesEncountered, null));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.EnemiesEncountered, 7));
        }

        [Fact]
        public void RecordBattleCompleted_RecordsBattleTimeAndSkillsUsedInSeconds()
        {
            var progress = MakeProgress();
            var stats = new BattleStats { PlayerSkillsUsed = 5 };

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 4500, stats,
                isBossBattle: false, zoneId: 0);

            // TotalBattleTime is tracked in seconds (ms / 1000).
            Assert.Equal(4.5m, progress.GetStatisticValue(EStatisticType.TotalBattleTime, null));
            Assert.Equal(5m, progress.GetStatisticValue(EStatisticType.SkillsUsed, null));
        }

        // ── RecordBattleCompleted: victory vs. loss ──────────────────────────

        [Fact]
        public void RecordBattleCompleted_Victory_TracksWinKillAndFastestVictory()
        {
            var progress = MakeProgress();
            var enemy = MakeEnemy(id: 3);

            progress.RecordBattleCompleted(enemy, victory: true, playerDied: false, totalMs: 6000, new BattleStats(),
                isBossBattle: false, zoneId: 0);

            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.BattlesWon, null));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.BattlesWon, 3));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.EnemiesKilled, null));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.EnemiesKilled, 3));
            // FastestVictory is recorded in seconds, globally and per-enemy.
            Assert.Equal(6m, progress.GetStatisticValue(EStatisticType.FastestVictory, null));
            Assert.Equal(6m, progress.GetStatisticValue(EStatisticType.FastestVictory, 3));
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.BattlesLost, null));
        }

        [Fact]
        public void RecordBattleCompleted_Loss_TracksLossButNotKillOrVictory()
        {
            var progress = MakeProgress();
            var enemy = MakeEnemy(id: 3);

            // A loss is a battle the player died in (not merely a non-victory).
            progress.RecordBattleCompleted(enemy, victory: false, playerDied: true, totalMs: 6000, new BattleStats(),
                isBossBattle: false, zoneId: 0);

            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.BattlesLost, null));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.BattlesLost, 3));
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.BattlesWon, null));
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.EnemiesKilled, null));
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.FastestVictory, null));
            // A loss is not an abandon.
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.BattlesAbandoned, null));
        }

        [Fact]
        public void RecordBattleCompleted_Abandon_TracksAbandonedNotLostWonOrKill()
        {
            var progress = MakeProgress();
            var enemy = MakeEnemy(id: 3);

            // Neither combatant died — the battle was abandoned mid-fight (#202).
            progress.RecordBattleCompleted(enemy, victory: false, playerDied: false, totalMs: 6000, new BattleStats(),
                isBossBattle: false, zoneId: 0);

            // Abandons are tracked globally and per-enemy, distinct from losses.
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.BattlesAbandoned, null));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.BattlesAbandoned, 3));
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.BattlesLost, null));
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.BattlesWon, null));
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.EnemiesKilled, null));
            // An abandon without a player death does not count as a death.
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.PlayerDeaths, null));
        }

        [Fact]
        public void RecordBattleCompleted_BossBattleVictory_IncrementsBossesDefeatedGloballyAndPerBoss()
        {
            var progress = MakeProgress();
            var boss = MakeEnemy(id: 9);

            progress.RecordBattleCompleted(boss, victory: true, playerDied: false, totalMs: 1000, new BattleStats(),
                isBossBattle: true, zoneId: 0);

            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.BossesDefeated, null));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.BossesDefeated, 9));
        }

        [Fact]
        public void RecordBattleCompleted_NonBossBattleVictory_DoesNotIncrementBossesDefeated()
        {
            var progress = MakeProgress();
            var enemy = MakeEnemy(id: 9);

            progress.RecordBattleCompleted(enemy, victory: true, playerDied: false, totalMs: 1000, new BattleStats(),
                isBossBattle: false, zoneId: 0);

            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.BossesDefeated, null));
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.BossesDefeated, 9));
        }

        [Fact]
        public void RecordBattleCompleted_BossBattleLoss_DoesNotIncrementBossesDefeated()
        {
            var progress = MakeProgress();

            progress.RecordBattleCompleted(MakeEnemy(), victory: false, playerDied: true, totalMs: 1000, new BattleStats(),
                isBossBattle: true, zoneId: 0);

            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.BossesDefeated, null));
        }

        // ── RecordBattleCompleted: zone clears (boss-battle victory) ─────────

        [Fact]
        public void RecordBattleCompleted_BossBattleVictory_IncrementsZonesClearedGloballyAndForBattleZone()
        {
            var progress = MakeProgress();

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000, new BattleStats(),
                isBossBattle: true, zoneId: 4);

            // A boss-battle victory clears the challenged zone, tracked globally and per-zone.
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.ZonesCleared, null));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.ZonesCleared, 4));
            // A different zone is unaffected — per-zone isolation.
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.ZonesCleared, 5));
        }

        [Fact]
        public void RecordBattleCompleted_NonBossBattleVictory_DoesNotIncrementZonesCleared()
        {
            var progress = MakeProgress();

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000, new BattleStats(),
                isBossBattle: false, zoneId: 4);

            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.ZonesCleared, null));
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.ZonesCleared, 4));
        }

        [Fact]
        public void RecordBattleCompleted_BossBattleLoss_DoesNotIncrementZonesCleared()
        {
            var progress = MakeProgress();

            progress.RecordBattleCompleted(MakeEnemy(), victory: false, playerDied: true, totalMs: 1000, new BattleStats(),
                isBossBattle: true, zoneId: 4);

            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.ZonesCleared, null));
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.ZonesCleared, 4));
        }

        [Fact]
        public void RecordBattleCompleted_RefarmingSameZoneBoss_LeavesZonesClearedButAccumulatesBossesDefeated()
        {
            var progress = MakeProgress();
            var boss = MakeEnemy(id: 9);

            progress.RecordBattleCompleted(boss, victory: true, playerDied: false, totalMs: 1000, new BattleStats(),
                isBossBattle: true, zoneId: 2);
            progress.RecordBattleCompleted(boss, victory: true, playerDied: false, totalMs: 1000, new BattleStats(),
                isBossBattle: true, zoneId: 2);

            // ZonesCleared is a distinct-zones-ever-cleared count: re-farming the same boss leaves both the
            // global counter and the zone's binary "cleared" flag at 1.
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.ZonesCleared, null));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.ZonesCleared, 2));
            // BossesDefeated is the farm counter: every victory bumps the global and per-boss totals.
            Assert.Equal(2m, progress.GetStatisticValue(EStatisticType.BossesDefeated, null));
            Assert.Equal(2m, progress.GetStatisticValue(EStatisticType.BossesDefeated, 9));
        }

        [Fact]
        public void RecordBattleCompleted_FirstClearsOfDifferentZones_AccumulateGlobalZonesCleared()
        {
            var progress = MakeProgress();

            progress.RecordBattleCompleted(MakeEnemy(id: 9), victory: true, playerDied: false, totalMs: 1000, new BattleStats(),
                isBossBattle: true, zoneId: 2);
            progress.RecordBattleCompleted(MakeEnemy(id: 10), victory: true, playerDied: false, totalMs: 1000, new BattleStats(),
                isBossBattle: true, zoneId: 3);

            // Each distinct zone's first clear bumps the global counter; each zone's flag is set to 1.
            Assert.Equal(2m, progress.GetStatisticValue(EStatisticType.ZonesCleared, null));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.ZonesCleared, 2));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.ZonesCleared, 3));
        }

        [Fact]
        public void RecordBattleCompleted_BossVictoryInAlreadyClearedZone_DoesNotRecountGlobalZonesCleared()
        {
            // First-clear is gated on the per-zone row's presence (the documented invariant), not a magic 0.
            // An aggregate loaded with the zone already cleared must not re-bump the global counter on a
            // re-farm — exercising the load-then-mutate path the in-session re-farm test doesn't.
            var progress = MakeProgress(statistics:
            [
                Stat(EStatisticType.ZonesCleared, null, 1m),
                Stat(EStatisticType.ZonesCleared, 2, 1m),
            ]);

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                new BattleStats(), isBossBattle: true, zoneId: 2);

            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.ZonesCleared, null));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.ZonesCleared, 2));
        }

        [Fact]
        public void RecordBattleCompleted_PlayerDied_IncrementsPlayerDeaths()
        {
            var progress = MakeProgress();

            progress.RecordBattleCompleted(MakeEnemy(), victory: false, playerDied: true, totalMs: 1000, new BattleStats(),
                isBossBattle: false, zoneId: 0);

            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.PlayerDeaths, null));
        }

        [Fact]
        public void RecordBattleCompleted_PlayerSurvived_DoesNotIncrementPlayerDeaths()
        {
            var progress = MakeProgress();

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000, new BattleStats(),
                isBossBattle: false, zoneId: 0);

            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.PlayerDeaths, null));
        }

        // ── RecordBattleCompleted: kills by damage type (#1455) ──────────────

        [Fact]
        public void RecordBattleCompleted_Victory_RecordsKillForMajorityTypeAndItsCategories()
        {
            var progress = MakeProgress();
            var stats = new BattleStats
            {
                TypedDamageDealt =
                {
                    [EDamageType.Fire] = 80.0,
                    [EDamageType.Physical] = 20.0,
                },
            };

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000, stats,
                isBossBattle: false, zoneId: 0);

            // Fire is the majority type — Applies(Fire) = [Fire, Elemental], so both keys book a kill.
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.KillsByDamageType, (int)EDamageTypeKey.Fire));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.KillsByDamageType, (int)EDamageTypeKey.Elemental));
            // The minority type (and its non-applicable keys) get no credit.
            Assert.False(progress.TryGetStatisticValue(EStatisticType.KillsByDamageType, (int)EDamageTypeKey.Physical, out _));
        }

        [Fact]
        public void RecordBattleCompleted_Victory_BurnMajority_RollsUpToAllFourKeys()
        {
            var progress = MakeProgress();
            var stats = new BattleStats { TypedDamageDealt = { [EDamageType.Burn] = 50.0 } };

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000, stats,
                isBossBattle: false, zoneId: 0);

            // Burn is fire, elemental, and DoT alike — Applies(Burn) = [Burn, Fire, Elemental, Dot].
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.KillsByDamageType, (int)EDamageTypeKey.Burn));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.KillsByDamageType, (int)EDamageTypeKey.Fire));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.KillsByDamageType, (int)EDamageTypeKey.Elemental));
            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.KillsByDamageType, (int)EDamageTypeKey.Dot));
        }

        [Fact]
        public void RecordBattleCompleted_Victory_TiedTypedDamage_BreaksTieOnLowerOrdinal()
        {
            var progress = MakeProgress();
            // Physical (ordinal 0) and Water (ordinal 2) tie — Physical wins the deterministic tie-break.
            var stats = new BattleStats
            {
                TypedDamageDealt =
                {
                    [EDamageType.Water] = 50.0,
                    [EDamageType.Physical] = 50.0,
                },
            };

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000, stats,
                isBossBattle: false, zoneId: 0);

            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.KillsByDamageType, (int)EDamageTypeKey.Physical));
            Assert.False(progress.TryGetStatisticValue(EStatisticType.KillsByDamageType, (int)EDamageTypeKey.Water, out _));
        }

        [Fact]
        public void RecordBattleCompleted_Victory_AllZeroTypedDamage_RecordsNoKillByDamageType()
        {
            // A fire-immune enemy killed entirely by untyped reflection: every fire portion books 0, so there is
            // no positive-damage entry to credit a kill to (#2170).
            var progress = MakeProgress();
            var stats = new BattleStats { TypedDamageDealt = { [EDamageType.Fire] = 0.0 } };

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000, stats,
                isBossBattle: false, zoneId: 0);

            Assert.False(progress.TryGetStatisticValue(EStatisticType.KillsByDamageType, (int)EDamageTypeKey.Fire, out _));
            Assert.False(progress.TryGetStatisticValue(EStatisticType.KillsByDamageType, (int)EDamageTypeKey.Elemental, out _));
        }

        [Fact]
        public void RecordBattleCompleted_Victory_ZeroValuedEntryIgnored_CreditsThePositiveMajorityInstead()
        {
            // A minority-but-positive type should still win over a larger zero-valued (immune) entry.
            var progress = MakeProgress();
            var stats = new BattleStats
            {
                TypedDamageDealt =
                {
                    [EDamageType.Fire] = 0.0,
                    [EDamageType.Physical] = 5.0,
                },
            };

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000, stats,
                isBossBattle: false, zoneId: 0);

            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.KillsByDamageType, (int)EDamageTypeKey.Physical));
            Assert.False(progress.TryGetStatisticValue(EStatisticType.KillsByDamageType, (int)EDamageTypeKey.Fire, out _));
        }

        [Fact]
        public void RecordBattleCompleted_Victory_NoTypedDamageDealt_RecordsNoKillByDamageType()
        {
            // Defensive branch: a victory with an empty offense book (shouldn't happen in practice, since a
            // kill requires dealing typed damage) must not throw or record a row.
            var progress = MakeProgress();

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                new BattleStats(), isBossBattle: false, zoneId: 0);

            Assert.False(progress.TryGetStatisticValue(EStatisticType.KillsByDamageType, (int)EDamageTypeKey.Physical, out _));
        }

        [Fact]
        public void RecordBattleCompleted_Loss_DoesNotRecordKillByDamageType()
        {
            var progress = MakeProgress();
            var stats = new BattleStats { TypedDamageDealt = { [EDamageType.Fire] = 30.0 } };

            progress.RecordBattleCompleted(MakeEnemy(), victory: false, playerDied: true, totalMs: 1000, stats,
                isBossBattle: false, zoneId: 0);

            Assert.False(progress.TryGetStatisticValue(EStatisticType.KillsByDamageType, (int)EDamageTypeKey.Fire, out _));
        }

        [Fact]
        public void RecordBattleCompleted_RepeatedFireKills_AccumulatesKillsByDamageType()
        {
            var progress = MakeProgress();
            var stats = new BattleStats { TypedDamageDealt = { [EDamageType.Fire] = 30.0 } };

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000, stats,
                isBossBattle: false, zoneId: 0);
            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000, stats,
                isBossBattle: false, zoneId: 0);

            Assert.Equal(2m, progress.GetStatisticValue(EStatisticType.KillsByDamageType, (int)EDamageTypeKey.Fire));
        }

        // ── RecordBattleCompleted: per-skill statistics ──────────────────────

        [Fact]
        public void RecordBattleCompleted_TracksPerSkillStatistics()
        {
            var progress = MakeProgress();
            var stats = new BattleStats
            {
                PlayerSkillsUsed = 3,
                SkillStats =
                {
                    [10] = new SkillStats { Uses = 2, TotalDamage = 60.0, HighestSingleAttack = 35.0 },
                    [20] = new SkillStats { Uses = 1, TotalDamage = 15.0, HighestSingleAttack = 15.0 },
                },
            };

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000, stats,
                isBossBattle: false, zoneId: 0);

            Assert.Equal(2m, progress.GetStatisticValue(EStatisticType.SkillsUsed, 10));
            Assert.Equal(60m, progress.GetStatisticValue(EStatisticType.DamageDealt, 10));
            Assert.Equal(35m, progress.GetStatisticValue(EStatisticType.HighestSingleAttackDamage, 10));

            Assert.Equal(1m, progress.GetStatisticValue(EStatisticType.SkillsUsed, 20));
            Assert.Equal(15m, progress.GetStatisticValue(EStatisticType.DamageDealt, 20));
        }

        // ── RecordBattleCompleted: accumulation across battles ───────────────

        [Fact]
        public void RecordBattleCompleted_AccumulatesAdditiveStatisticsAcrossBattles()
        {
            var progress = MakeProgress();
            var enemy = MakeEnemy(id: 1);

            progress.RecordBattleCompleted(enemy, victory: true, playerDied: false, totalMs: 2000,
                new BattleStats { PlayerDamageDealt = 50.0 }, isBossBattle: false, zoneId: 0);
            progress.RecordBattleCompleted(enemy, victory: true, playerDied: false, totalMs: 3000,
                new BattleStats { PlayerDamageDealt = 70.0 }, isBossBattle: false, zoneId: 0);

            Assert.Equal(2m, progress.GetStatisticValue(EStatisticType.EnemiesKilled, null));
            Assert.Equal(2m, progress.GetStatisticValue(EStatisticType.EnemiesEncountered, null));
            Assert.Equal(120m, progress.GetStatisticValue(EStatisticType.DamageDealt, null));
            Assert.Equal(5m, progress.GetStatisticValue(EStatisticType.TotalBattleTime, null));
        }

        [Fact]
        public void RecordBattleCompleted_KeepsMaximumHighestSingleAttack()
        {
            var progress = MakeProgress();

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                new BattleStats { HighestPlayerAttack = 40.0 }, isBossBattle: false, zoneId: 0);
            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                new BattleStats { HighestPlayerAttack = 25.0 }, isBossBattle: false, zoneId: 0);

            Assert.Equal(40m, progress.GetStatisticValue(EStatisticType.HighestSingleAttackDamage, null));
        }

        [Fact]
        public void RecordBattleCompleted_KeepsMinimumFastestVictory()
        {
            var progress = MakeProgress();
            var enemy = MakeEnemy(id: 1);

            progress.RecordBattleCompleted(enemy, victory: true, playerDied: false, totalMs: 5000, new BattleStats(),
                isBossBattle: false, zoneId: 0);
            progress.RecordBattleCompleted(enemy, victory: true, playerDied: false, totalMs: 3000, new BattleStats(),
                isBossBattle: false, zoneId: 0);
            progress.RecordBattleCompleted(enemy, victory: true, playerDied: false, totalMs: 8000, new BattleStats(),
                isBossBattle: false, zoneId: 0);

            Assert.Equal(3m, progress.GetStatisticValue(EStatisticType.FastestVictory, null));
        }

        [Fact]
        public void RecordBattleCompleted_MutatorMatchesStatisticAggregationKind()
        {
            // The mutator is now derived from StatisticType.AggregationKind, not chosen by hand per call.
            // Recording two values across two battles must agree with that direction for each kind:
            // Sum accumulates, Max keeps the larger, Min keeps the smaller.
            var sumProgress = MakeProgress();
            var maxProgress = MakeProgress();
            var minProgress = MakeProgress();

            // Sum: DamageDealt (40 then 60 → 100).
            sumProgress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                new BattleStats { PlayerDamageDealt = 40.0 }, isBossBattle: false, zoneId: 0);
            sumProgress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                new BattleStats { PlayerDamageDealt = 60.0 }, isBossBattle: false, zoneId: 0);

            // Max: HighestSingleAttackDamage (30 then 20 → 30).
            maxProgress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                new BattleStats { HighestPlayerAttack = 30.0 }, isBossBattle: false, zoneId: 0);
            maxProgress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                new BattleStats { HighestPlayerAttack = 20.0 }, isBossBattle: false, zoneId: 0);

            // Min: FastestVictory (7s then 5s → 5s).
            minProgress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 7000,
                new BattleStats(), isBossBattle: false, zoneId: 0);
            minProgress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 5000,
                new BattleStats(), isBossBattle: false, zoneId: 0);

            Assert.Equal(EAggregationKind.Sum, StatisticType.GetAggregationKind(EStatisticType.DamageDealt));
            Assert.Equal(100m, sumProgress.GetStatisticValue(EStatisticType.DamageDealt, null));

            Assert.Equal(EAggregationKind.Max, StatisticType.GetAggregationKind(EStatisticType.HighestSingleAttackDamage));
            Assert.Equal(30m, maxProgress.GetStatisticValue(EStatisticType.HighestSingleAttackDamage, null));

            Assert.Equal(EAggregationKind.Min, StatisticType.GetAggregationKind(EStatisticType.FastestVictory));
            Assert.Equal(5m, minProgress.GetStatisticValue(EStatisticType.FastestVictory, null));
        }

        [Fact]
        public void RecordBattleCompleted_FirstVictoryWithZeroDuration_RecordsZeroFastestVictory()
        {
            var progress = MakeProgress();

            // An instant (0ms) victory is a legitimate FastestVictory of 0, not "no data".
            progress.RecordBattleCompleted(MakeEnemy(id: 1), victory: true, playerDied: false, totalMs: 0, new BattleStats(),
                isBossBattle: false, zoneId: 0);

            Assert.True(progress.TryGetStatisticValue(EStatisticType.FastestVictory, null, out var value));
            Assert.Equal(0m, value);
        }

        [Fact]
        public void RecordBattleCompleted_ZeroFastestVictory_IsNotOverwrittenBySlowerVictory()
        {
            // Regression for the "0 means no value yet" sentinel: once a 0 minimum is recorded a slower
            // later victory must not overwrite it (previously stat.Value == 0 pinned the stat forever).
            var progress = MakeProgress();
            var enemy = MakeEnemy(id: 1);

            progress.RecordBattleCompleted(enemy, victory: true, playerDied: false, totalMs: 0, new BattleStats(),
                isBossBattle: false, zoneId: 0);
            progress.RecordBattleCompleted(enemy, victory: true, playerDied: false, totalMs: 4000, new BattleStats(),
                isBossBattle: false, zoneId: 0);

            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.FastestVictory, null));
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.FastestVictory, 1));
        }

        // ── GetStatisticValue / TryGetStatisticValue ─────────────────────────

        [Fact]
        public void GetStatisticValue_UntrackedStatistic_ReturnsZero()
        {
            var progress = MakeProgress();

            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.DamageDealt, null));
            Assert.Equal(0m, progress.GetStatisticValue(EStatisticType.EnemiesKilled, 999));
        }

        [Fact]
        public void TryGetStatisticValue_UntrackedStatistic_ReturnsFalseAndZero()
        {
            var progress = MakeProgress();

            Assert.False(progress.TryGetStatisticValue(EStatisticType.FastestVictory, null, out var value));
            Assert.Equal(0m, value);
        }

        [Fact]
        public void TryGetStatisticValue_RecordedZero_ReturnsTrueAndZero()
        {
            // A recorded 0 is data, not absence — the distinction the AtMost challenge path relies on.
            var progress = MakeProgress(statistics: [Stat(EStatisticType.FastestVictory, null, 0m)]);

            Assert.True(progress.TryGetStatisticValue(EStatisticType.FastestVictory, null, out var value));
            Assert.Equal(0m, value);
        }

        // ── EvaluateChallenges ───────────────────────────────────────────────

        [Fact]
        public void EvaluateChallenges_StatisticMeetsGoal_CompletesAndReturnsReward()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.EnemiesKilled, goal: 5, rewardItemId: 42, rewardItemModId: 7);
            var progress = MakeProgress(statistics:
            [
                Stat(EStatisticType.EnemiesKilled, null, 5m),
            ]);

            var completed = progress.EvaluateChallenges([challenge], Timestamp);

            var result = Assert.Single(completed);
            Assert.Equal(0, result.ChallengeId);
            Assert.Equal(42, result.RewardItemId);
            Assert.Equal(7, result.RewardItemModId);

            var playerChallenge = Assert.Single(progress.ChallengeProgress);
            Assert.True(playerChallenge.Completed);
            Assert.Equal(Timestamp, playerChallenge.CompletedAt);
            Assert.Equal(5m, playerChallenge.Progress);
        }

        [Fact]
        public void EvaluateChallenges_StatisticBelowGoal_TracksProgressWithoutCompleting()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.EnemiesKilled, goal: 10);
            var progress = MakeProgress(statistics:
            [
                Stat(EStatisticType.EnemiesKilled, null, 3m),
            ]);

            var completed = progress.EvaluateChallenges([challenge], Timestamp);

            Assert.Empty(completed);
            var playerChallenge = Assert.Single(progress.ChallengeProgress);
            Assert.False(playerChallenge.Completed);
            Assert.Null(playerChallenge.CompletedAt);
            Assert.Equal(3m, playerChallenge.Progress);
        }

        [Fact]
        public void EvaluateChallenges_StatisticExceedsGoal_ClampsProgressToGoal()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.EnemiesKilled, goal: 5);
            var progress = MakeProgress(statistics:
            [
                Stat(EStatisticType.EnemiesKilled, null, 12m),
            ]);

            progress.EvaluateChallenges([challenge], Timestamp);

            var playerChallenge = Assert.Single(progress.ChallengeProgress);
            Assert.True(playerChallenge.Completed);
            Assert.Equal(5m, playerChallenge.Progress);
        }

        [Fact]
        public void EvaluateChallenges_AlreadyCompletedChallenge_IsNotReReported()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.EnemiesKilled, goal: 5);
            var alreadyCompleted = new PlayerChallenge(challenge, progress: 5m, completed: true, completedAt: DateTime.UtcNow);
            var progress = MakeProgress(
                statistics: [Stat(EStatisticType.EnemiesKilled, null, 12m)],
                challenges: [alreadyCompleted]);

            var completed = progress.EvaluateChallenges([challenge], Timestamp);

            Assert.Empty(completed);
        }

        [Fact]
        public void EvaluateChallenges_RetiredIncompleteChallenge_IsSkippedEvenWhenGoalMet()
        {
            // A retired challenge the player has not already completed is out of circulation: even with a
            // statistic that meets the goal, it neither completes nor records a progress row.
            var challenge = MakeChallenge(id: 0, EChallengeType.EnemiesKilled, goal: 5, retiredAt: DateTime.UtcNow);
            var progress = MakeProgress(statistics: [Stat(EStatisticType.EnemiesKilled, null, 12m)]);

            var completed = progress.EvaluateChallenges([challenge], Timestamp);

            Assert.Empty(completed);
            Assert.Empty(progress.ChallengeProgress);
            Assert.Empty(progress.DirtyChallenges);
        }

        [Fact]
        public void EvaluateChallenges_RetiredChallengeAlreadyCompleted_KeepsItsCompletion()
        {
            // Retirement never revokes an existing completion (or its reward): an already-completed retired
            // challenge keeps its row and is simply not re-reported.
            var challenge = MakeChallenge(id: 0, EChallengeType.EnemiesKilled, goal: 5, retiredAt: DateTime.UtcNow);
            var alreadyCompleted = new PlayerChallenge(challenge, progress: 5m, completed: true, completedAt: DateTime.UtcNow);
            var progress = MakeProgress(
                statistics: [Stat(EStatisticType.EnemiesKilled, null, 12m)],
                challenges: [alreadyCompleted]);

            var completed = progress.EvaluateChallenges([challenge], Timestamp);

            Assert.Empty(completed);
            Assert.True(Assert.Single(progress.ChallengeProgress).Completed);
        }

        [Fact]
        public void EvaluateChallenges_PerEntityChallenge_UsesPerEntityStatistic()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.EnemiesKilled, goal: 3, targetEntityId: 2);
            var progress = MakeProgress(statistics:
            [
                Stat(EStatisticType.EnemiesKilled, null, 100m), // global total should be ignored
                Stat(EStatisticType.EnemiesKilled, 2, 3m),
            ]);

            var completed = progress.EvaluateChallenges([challenge], Timestamp);

            Assert.Single(completed);
        }

        [Fact]
        public void EvaluateChallenges_ZonesCleared_CompletesAfterBossVictoryInTargetZone()
        {
            // Regression test for the bug where ZonesCleared was never written, so a
            // ZonesCleared challenge could never complete. A boss victory in the target zone
            // should now satisfy it.
            var challenge = MakeChallenge(id: 0, EChallengeType.ZonesCleared, goal: 1, targetEntityId: 3);
            var progress = MakeProgress();

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000, new BattleStats(),
                isBossBattle: true, zoneId: 3);
            var completed = progress.EvaluateChallenges([challenge], Timestamp);

            Assert.Single(completed);
            Assert.True(Assert.Single(progress.ChallengeProgress).Completed);
        }

        [Fact]
        public void EvaluateChallenges_LevelReached_UsesPlayerLevel()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.LevelReached, goal: 5);
            var progress = MakeProgress(player: new PlayerBuilder().WithLevel(5).Build());

            var completed = progress.EvaluateChallenges([challenge], Timestamp);

            Assert.Single(completed);
        }

        [Fact]
        public void EvaluateChallenges_LevelReachedBelowGoal_DoesNotComplete()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.LevelReached, goal: 10);
            var progress = MakeProgress(player: new PlayerBuilder().WithLevel(5).Build());

            var completed = progress.EvaluateChallenges([challenge], Timestamp);

            Assert.Empty(completed);
            Assert.Equal(5m, Assert.Single(progress.ChallengeProgress).Progress);
        }

        [Fact]
        public void EvaluateChallenges_TimeTrial_CompletesWhenFastestVictoryAtOrBelowGoal()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.TimeTrial, goal: 10);
            var progress = MakeProgress(statistics:
            [
                Stat(EStatisticType.FastestVictory, null, 8m), // best victory in 8s, goal is "within 10s"
            ]);

            var completed = progress.EvaluateChallenges([challenge], Timestamp);

            Assert.Single(completed);
            Assert.True(Assert.Single(progress.ChallengeProgress).Completed);
        }

        [Fact]
        public void EvaluateChallenges_TimeTrial_DoesNotCompleteWhenFastestVictoryAboveGoal()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.TimeTrial, goal: 10);
            var progress = MakeProgress(statistics:
            [
                Stat(EStatisticType.FastestVictory, null, 14m),
            ]);

            var completed = progress.EvaluateChallenges([challenge], Timestamp);

            Assert.Empty(completed);
        }

        [Fact]
        public void EvaluateChallenges_TimeTrial_DoesNotCompleteWithNoRecordedVictory()
        {
            var challenge = MakeChallenge(id: 0, EChallengeType.TimeTrial, goal: 10);
            var progress = MakeProgress(); // no FastestVictory statistic recorded yet

            var completed = progress.EvaluateChallenges([challenge], Timestamp);

            Assert.Empty(completed);
        }

        [Fact]
        public void EvaluateChallenges_TimeTrial_CompletesWhenFastestVictoryIsRecordedZero()
        {
            // A recorded FastestVictory of 0 (e.g. an instant victory) is a real value at or below the
            // goal, so it must complete — distinct from the "no victory yet" case above.
            var challenge = MakeChallenge(id: 0, EChallengeType.TimeTrial, goal: 10);
            var progress = MakeProgress(statistics:
            [
                Stat(EStatisticType.FastestVictory, null, 0m),
            ]);

            var completed = progress.EvaluateChallenges([challenge], Timestamp);

            Assert.Single(completed);
            Assert.True(Assert.Single(progress.ChallengeProgress).Completed);
        }

        [Fact]
        public void EvaluateChallenges_NewlyRelevantChallengeWithNoProgress_PersistsNoInfoFreeRow()
        {
            // A challenge that becomes relevant but gains no progress this battle must not commit an
            // information-free (zero-progress, incomplete) row: it stays out of both the write-behind
            // persist set and the cached snapshot exposed via ChallengeProgress.
            var challenge = MakeChallenge(id: 0, EChallengeType.EnemiesKilled, goal: 5);
            var progress = MakeProgress(); // no EnemiesKilled statistic recorded yet

            var completed = progress.EvaluateChallenges([challenge], Timestamp);

            Assert.Empty(completed);
            Assert.Empty(progress.DirtyChallenges);
            Assert.Empty(progress.ChallengeProgress);
        }

        [Fact]
        public void EvaluateChallenges_MultipleChallenges_ReturnsOnlyCompletedOnes()
        {
            var met = MakeChallenge(id: 0, EChallengeType.EnemiesKilled, goal: 5);
            var unmet = MakeChallenge(id: 1, EChallengeType.BattlesWon, goal: 100);
            var progress = MakeProgress(statistics:
            [
                Stat(EStatisticType.EnemiesKilled, null, 5m),
                Stat(EStatisticType.BattlesWon, null, 2m),
            ]);

            var completed = progress.EvaluateChallenges([met, unmet], Timestamp);

            Assert.Equal(0, Assert.Single(completed).ChallengeId);
        }

        // ── Dirty tracking (write-behind persist set) ────────────────────────

        [Fact]
        public void DirtyStatistics_OnFreshlyLoadedProgress_IsEmpty()
        {
            // Statistics supplied at construction (a cache/DB load) are not dirty — only mutations are.
            var progress = MakeProgress(statistics: [Stat(EStatisticType.EnemiesKilled, null, 5m)]);

            Assert.Empty(progress.DirtyStatistics);
            Assert.Empty(progress.DirtyChallenges);
        }

        [Fact]
        public void GetStatisticValue_DoesNotMarkDirty()
        {
            var progress = MakeProgress(statistics: [Stat(EStatisticType.EnemiesKilled, null, 5m)]);

            progress.GetStatisticValue(EStatisticType.EnemiesKilled, null);

            // A read must never enter the persist set.
            Assert.Empty(progress.DirtyStatistics);
        }

        [Fact]
        public void AcceptChanges_ClearsAllThreeDirtySets()
        {
            var progress = MakeProgress();
            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000,
                new BattleStats { PlayerDamageDealt = 10.0 }, isBossBattle: false, zoneId: 0);
            progress.EvaluateChallenges([MakeChallenge(id: 0, EChallengeType.EnemiesKilled, goal: 5)], Timestamp);
            progress.SetProficiencyProgress(proficiencyId: 3, level: 1, xp: 40m);

            progress.AcceptChanges();

            Assert.Empty(progress.DirtyStatistics);
            Assert.Empty(progress.DirtyChallenges);
            Assert.Empty(progress.DirtyProficiencies);
        }

        [Fact]
        public void AcceptChanges_ThenMutatingAgain_DirtiesOnlyTheNewMutation()
        {
            // Simulates a saved aggregate reused for a second mutation: the first save's rows must not
            // resurface in the persist set once a new, unrelated statistic is touched.
            var progress = MakeProgress();
            progress.RecordBattleCompleted(MakeEnemy(id: 1), victory: true, playerDied: false, totalMs: 1000,
                new BattleStats { PlayerDamageDealt = 10.0 }, isBossBattle: false, zoneId: 0);
            progress.AcceptChanges();

            progress.SetProficiencyProgress(proficiencyId: 3, level: 1, xp: 40m);

            Assert.Empty(progress.DirtyStatistics);
            Assert.Equal(3, Assert.Single(progress.DirtyProficiencies).ProficiencyId);
        }

        [Fact]
        public void RecordBattleCompleted_MarksOnlyTheTouchedStatisticsDirty()
        {
            var progress = MakeProgress();

            progress.RecordBattleCompleted(MakeEnemy(id: 3), victory: true, playerDied: false, totalMs: 1000,
                new BattleStats { PlayerDamageDealt = 10.0 }, isBossBattle: false, zoneId: 0);

            // The rows the battle touched are dirty (e.g. the global kill counter and its per-enemy twin)...
            Assert.Contains(progress.DirtyStatistics, s => s.Type == EStatisticType.EnemiesKilled && s.EntityId == null);
            Assert.Contains(progress.DirtyStatistics, s => s.Type == EStatisticType.EnemiesKilled && s.EntityId == 3);
            // ...while stats this battle never touched stay out of the persist set (BattlesLost on a win)...
            Assert.DoesNotContain(progress.DirtyStatistics, s => s.Type == EStatisticType.BattlesLost);
            Assert.DoesNotContain(progress.DirtyStatistics, s => s.Type == EStatisticType.BattlesWon && s.EntityId == 999);
            // ...and a Sum statistic reported as 0 (zero DamageTaken this battle) is not "touched" at all —
            // a zero delta is skipped outright, so it never enters the persist set (#1515).
            Assert.DoesNotContain(progress.DirtyStatistics, s => s.Type == EStatisticType.DamageTaken);
        }

        [Fact]
        public void RecordBattleCompleted_ZeroDeltaSumStatistics_CreateNoRowAndAreNotDirtiedOrTouched()
        {
            // A battle with no crits/dodges/parries/heals reports the whole family as 0. A zero-delta Sum
            // record carries no information (row absence is the "no data" signal), so it creates no row,
            // enters no persist set, and returns no touched key re-evaluating its challenges (#1515).
            var progress = MakeProgress();

            var touched = progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false,
                totalMs: 1000, new BattleStats(), isBossBattle: false, zoneId: 0);

            EStatisticType[] zeroDeltaSums =
            [
                EStatisticType.DamageDealt, EStatisticType.DamageTaken, EStatisticType.DamageHealed,
                EStatisticType.CriticalHits, EStatisticType.CriticalDamageDealt,
                EStatisticType.AttacksDodged, EStatisticType.DamageDodged,
                EStatisticType.AttacksParried, EStatisticType.DamageParried,
                EStatisticType.CounterDamageDealt, EStatisticType.SkillsUsed,
            ];
            foreach (var type in zeroDeltaSums)
            {
                Assert.False(progress.TryGetStatisticValue(type, null, out _));
                Assert.DoesNotContain(progress.DirtyStatistics, s => s.Type == type);
                Assert.DoesNotContain((type, (int?)null), touched);
            }
        }

        [Fact]
        public void RecordBattleCompleted_ZeroDeltaSumOnExistingRow_LeavesItCleanAndUntouched()
        {
            // An aggregate loaded with a prior crit count records a crit-less battle: the existing row's
            // value is unchanged, so it must stay out of both the persist set and the touched keys.
            var progress = MakeProgress(statistics: [Stat(EStatisticType.CriticalHits, null, 5m)]);

            var touched = progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false,
                totalMs: 1000, new BattleStats(), isBossBattle: false, zoneId: 0);

            Assert.Equal(5m, progress.GetStatisticValue(EStatisticType.CriticalHits, null));
            Assert.DoesNotContain(progress.DirtyStatistics, s => s.Type == EStatisticType.CriticalHits);
            Assert.DoesNotContain((EStatisticType.CriticalHits, (int?)null), touched);
        }

        [Fact]
        public void RecordBattleCompleted_NonZeroSumDelta_IsRecordedDirtiedAndTouched()
        {
            // The zero-delta skip must not swallow real deltas: a battle with crits records, dirties, and
            // touches the stat so its challenges are re-evaluated.
            var progress = MakeProgress();

            var touched = progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false,
                totalMs: 1000, new BattleStats { CriticalHits = 2 }, isBossBattle: false, zoneId: 0);

            Assert.Equal(2m, progress.GetStatisticValue(EStatisticType.CriticalHits, null));
            Assert.Contains(progress.DirtyStatistics, s => s.Type == EStatisticType.CriticalHits && s.EntityId == null);
            Assert.Contains((EStatisticType.CriticalHits, (int?)null), touched);
        }

        [Fact]
        public void RecordBattleCompleted_ZeroFirstWriteMinStatistic_StillCreatesAndDirtiesItsRow()
        {
            // Min first-write semantics are untouched by the Max fix below: a first recorded 0 (an instant
            // victory) is a genuine value a Min stat must lock in, so the row is created and persisted.
            var progress = MakeProgress();

            var touched = progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false,
                totalMs: 0, new BattleStats(), isBossBattle: false, zoneId: 0);

            Assert.True(progress.TryGetStatisticValue(EStatisticType.FastestVictory, null, out var fastest));
            Assert.Equal(0m, fastest);
            Assert.Contains(progress.DirtyStatistics, s => s.Type == EStatisticType.FastestVictory && s.EntityId == null);
            Assert.Contains((EStatisticType.FastestVictory, (int?)null), touched);
        }

        [Fact]
        public void RecordBattleCompleted_ZeroFirstWriteMaxStatistic_LeavesItAbsentAndUntouched()
        {
            // Unlike Min, a Max stat has no legitimate 0 to lock in: the true first positive value would
            // beat a fresh row's 0 anyway. So a first-write 0 (a battle with no player attack) carries no
            // information and, like the zero-delta Sum skip, must not create a row or enter the touched set.
            var progress = MakeProgress();

            var touched = progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false,
                totalMs: 0, new BattleStats(), isBossBattle: false, zoneId: 0);

            Assert.False(progress.TryGetStatisticValue(EStatisticType.HighestSingleAttackDamage, null, out _));
            Assert.DoesNotContain(progress.DirtyStatistics, s => s.Type == EStatisticType.HighestSingleAttackDamage);
            Assert.DoesNotContain((EStatisticType.HighestSingleAttackDamage, (int?)null), touched);
        }

        [Fact]
        public void RecordBattleCompleted_UnimprovedMaxStatisticOnExistingRow_LeavesItCleanAndUntouched()
        {
            // An aggregate loaded with a prior HighestSingleAttackDamage records a battle whose attack
            // doesn't beat it: the existing row's value is unchanged, so — like the zero-delta Sum case —
            // it must stay out of both the persist set and the touched keys, not just get a no-op re-mark.
            var progress = MakeProgress(statistics: [Stat(EStatisticType.HighestSingleAttackDamage, null, 40m)]);

            var touched = progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false,
                totalMs: 1000, new BattleStats { HighestPlayerAttack = 25.0 }, isBossBattle: false, zoneId: 0);

            Assert.Equal(40m, progress.GetStatisticValue(EStatisticType.HighestSingleAttackDamage, null));
            Assert.DoesNotContain(progress.DirtyStatistics, s => s.Type == EStatisticType.HighestSingleAttackDamage);
            Assert.DoesNotContain((EStatisticType.HighestSingleAttackDamage, (int?)null), touched);
        }

        [Fact]
        public void RecordBattleCompleted_UnimprovedMinStatisticOnExistingRow_LeavesItCleanAndUntouched()
        {
            // An aggregate loaded with a prior FastestVictory (global and per-enemy) records a slower
            // victory against the same enemy: the existing rows' values are unchanged, so they must stay
            // out of both the persist set and the touched keys — the Min/Max analogue of the zero-delta Sum
            // skip (#1821).
            var enemy = MakeEnemy(id: 1);
            var progress = MakeProgress(statistics:
            [
                Stat(EStatisticType.FastestVictory, null, 3m),
                Stat(EStatisticType.FastestVictory, 1, 3m),
            ]);

            var touched = progress.RecordBattleCompleted(enemy, victory: true, playerDied: false,
                totalMs: 8000, new BattleStats(), isBossBattle: false, zoneId: 0);

            Assert.Equal(3m, progress.GetStatisticValue(EStatisticType.FastestVictory, null));
            Assert.Equal(3m, progress.GetStatisticValue(EStatisticType.FastestVictory, 1));
            Assert.DoesNotContain(progress.DirtyStatistics, s => s.Type == EStatisticType.FastestVictory);
            Assert.DoesNotContain((EStatisticType.FastestVictory, (int?)null), touched);
            Assert.DoesNotContain((EStatisticType.FastestVictory, (int?)1), touched);
        }

        [Fact]
        public void RecordBattleCompleted_ImprovedMaxStatisticOnExistingRow_IsRecordedDirtiedAndTouched()
        {
            // The unimproved-value skip must not swallow a real improvement: a battle that beats the prior
            // HighestSingleAttackDamage still records, dirties, and touches the stat.
            var progress = MakeProgress(statistics: [Stat(EStatisticType.HighestSingleAttackDamage, null, 20m)]);

            var touched = progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false,
                totalMs: 1000, new BattleStats { HighestPlayerAttack = 35.0 }, isBossBattle: false, zoneId: 0);

            Assert.Equal(35m, progress.GetStatisticValue(EStatisticType.HighestSingleAttackDamage, null));
            Assert.Contains(progress.DirtyStatistics, s => s.Type == EStatisticType.HighestSingleAttackDamage && s.EntityId == null);
            Assert.Contains((EStatisticType.HighestSingleAttackDamage, (int?)null), touched);
        }

        [Fact]
        public void RecordBattleCompleted_ZeroDamageSkill_RecordsItsUsesButNoDamageDealtRow()
        {
            // A used utility skill that dealt no damage: the per-skill use count is a real delta, while both
            // its per-skill DamageDealt Sum and HighestSingleAttackDamage Max twin stay absent — neither has
            // a legitimate 0 to record.
            var progress = MakeProgress();
            var stats = new BattleStats
            {
                PlayerSkillsUsed = 2,
                SkillStats = { [10] = new SkillStats { Uses = 2, TotalDamage = 0.0, HighestSingleAttack = 0.0 } },
            };

            progress.RecordBattleCompleted(MakeEnemy(), victory: true, playerDied: false, totalMs: 1000, stats,
                isBossBattle: false, zoneId: 0);

            Assert.Equal(2m, progress.GetStatisticValue(EStatisticType.SkillsUsed, 10));
            Assert.False(progress.TryGetStatisticValue(EStatisticType.DamageDealt, 10, out _));
            Assert.False(progress.TryGetStatisticValue(EStatisticType.HighestSingleAttackDamage, 10, out _));
        }

        [Fact]
        public void RecordBattleCompleted_ReturnsTheTouchedStatisticKeys()
        {
            var progress = MakeProgress();

            var touched = progress.RecordBattleCompleted(MakeEnemy(id: 3), victory: true, playerDied: false,
                totalMs: 1000, new BattleStats { PlayerDamageDealt = 10.0 }, isBossBattle: false, zoneId: 0);

            // The returned keys are exactly the rows the battle touched — the relevance scope a caller hands
            // to ChallengeIndex.RelevantTo — so the global kill counter and its per-enemy twin are present...
            Assert.Contains((EStatisticType.EnemiesKilled, (int?)null), touched);
            Assert.Contains((EStatisticType.EnemiesKilled, (int?)3), touched);
            // ...and a stat this winning battle never touched (BattlesLost) is absent.
            Assert.DoesNotContain((EStatisticType.BattlesLost, (int?)null), touched);
            // The returned set mirrors the dirty (persist) set on a freshly loaded aggregate.
            Assert.Equal(
                progress.DirtyStatistics.Select(s => (s.Type, s.EntityId)).ToHashSet(),
                touched.ToHashSet());
        }

        [Fact]
        public void RecordBattleCompleted_SecondCallOnSameAggregate_ReturnsOnlyThatCallsOwnDelta()
        {
            // The offline path reuses one aggregate across a whole away window without AcceptChanges between
            // battles (#2209), so the touched set returned by each call must be that battle's own delta —
            // not the aggregate's whole dirty set accumulated since load.
            var progress = MakeProgress();
            var enemy = MakeEnemy(id: 5);

            var firstTouched = progress.RecordBattleCompleted(enemy, victory: true, playerDied: false,
                totalMs: 1000, new BattleStats { CriticalHits = 2 }, isBossBattle: false, zoneId: 0);
            var secondTouched = progress.RecordBattleCompleted(enemy, victory: true, playerDied: false,
                totalMs: 1000, new BattleStats(), isBossBattle: false, zoneId: 0);

            // CriticalHits was touched by the first, crit-having battle and stays dirty (unsaved) into the
            // second call, but the second, crit-less battle never touched it and must not re-report it.
            Assert.Contains((EStatisticType.CriticalHits, (int?)null), firstTouched);
            Assert.Contains(progress.DirtyStatistics, s => s.Type == EStatisticType.CriticalHits);
            Assert.DoesNotContain((EStatisticType.CriticalHits, (int?)null), secondTouched);
        }

        [Fact]
        public void EvaluateChallenges_MarksNewAndChangedChallengesDirty_ButNotSkippedCompletedOnes()
        {
            var progressed = MakeChallenge(id: 0, EChallengeType.EnemiesKilled, goal: 5);
            var alreadyDone = MakeChallenge(id: 1, EChallengeType.EnemiesKilled, goal: 5);
            var doneRow = new PlayerChallenge(alreadyDone, progress: 5m, completed: true, completedAt: DateTime.UtcNow);
            var progress = MakeProgress(
                statistics: [Stat(EStatisticType.EnemiesKilled, null, 5m)],
                challenges: [doneRow]);

            progress.EvaluateChallenges([progressed, alreadyDone], Timestamp);

            // The challenge that advanced this evaluation is dirty; the already-completed one is skipped untouched.
            Assert.Contains(0, progress.DirtyChallenges.Select(c => c.Challenge.Id));
            Assert.DoesNotContain(1, progress.DirtyChallenges.Select(c => c.Challenge.Id));
        }

        // ── Proficiencies (level/XP progress + dirty tracking) ───────────────

        [Fact]
        public void TryGetProficiency_UntrackedProficiency_ReturnsFalse()
        {
            var progress = MakeProgress();

            Assert.False(progress.TryGetProficiency(3, out var proficiency));
            Assert.Null(proficiency);
        }

        [Fact]
        public void TryGetProficiency_TrackedProficiency_ReturnsTrueAndRow()
        {
            var progress = MakeProgress(proficiencies: [Prof(proficiencyId: 3, level: 2, xp: 150m)]);

            Assert.True(progress.TryGetProficiency(3, out var proficiency));
            Assert.NotNull(proficiency);
            Assert.Equal(2, proficiency.Level);
            Assert.Equal(150m, proficiency.Xp);
        }

        [Fact]
        public void DirtyProficiencies_OnFreshlyLoadedProgress_IsEmpty()
        {
            // Proficiencies supplied at construction (a cache/DB load) are not dirty — only mutations are.
            var progress = MakeProgress(proficiencies: [Prof(proficiencyId: 3, level: 2, xp: 150m)]);

            Assert.Empty(progress.DirtyProficiencies);
        }

        [Fact]
        public void TryGetProficiency_DoesNotMarkDirty()
        {
            var progress = MakeProgress(proficiencies: [Prof(proficiencyId: 3, level: 2, xp: 150m)]);

            progress.TryGetProficiency(3, out _);

            // A read must never enter the persist set.
            Assert.Empty(progress.DirtyProficiencies);
        }

        [Fact]
        public void SetProficiencyProgress_NewProficiency_AddsRowAndMarksDirty()
        {
            var progress = MakeProgress();

            progress.SetProficiencyProgress(proficiencyId: 3, level: 1, xp: 40m);

            var proficiency = Assert.Single(progress.Proficiencies);
            Assert.Equal(3, proficiency.ProficiencyId);
            Assert.Equal(1, proficiency.Level);
            Assert.Equal(40m, proficiency.Xp);
            var dirty = Assert.Single(progress.DirtyProficiencies);
            Assert.Equal(3, dirty.ProficiencyId);
        }

        [Fact]
        public void SetProficiencyProgress_ExistingProficiency_UpdatesInPlaceAndMarksDirty()
        {
            var progress = MakeProgress(proficiencies: [Prof(proficiencyId: 3, level: 1, xp: 40m)]);

            progress.SetProficiencyProgress(proficiencyId: 3, level: 2, xp: 130m);

            // The existing row is updated in place (no duplicate) and enters the persist set.
            var proficiency = Assert.Single(progress.Proficiencies);
            Assert.Equal(2, proficiency.Level);
            Assert.Equal(130m, proficiency.Xp);
            Assert.Equal(3, Assert.Single(progress.DirtyProficiencies).ProficiencyId);
        }

        [Fact]
        public void SetProficiencyProgress_MarksOnlyTheTouchedProficiencyDirty()
        {
            var progress = MakeProgress(proficiencies:
            [
                Prof(proficiencyId: 3, level: 1, xp: 40m),
                Prof(proficiencyId: 4, level: 0, xp: 10m),
            ]);

            progress.SetProficiencyProgress(proficiencyId: 3, level: 2, xp: 130m);

            // Only the mutated proficiency is dirty; the untouched one stays out of the persist set.
            Assert.Contains(progress.DirtyProficiencies, p => p.ProficiencyId == 3);
            Assert.DoesNotContain(progress.DirtyProficiencies, p => p.ProficiencyId == 4);
        }

        // ── CompletedChallengeIds ────────────────────────────────────────────

        [Fact]
        public void CompletedChallengeIds_ReturnsOnlyCompletedChallenges()
        {
            var completed = new PlayerChallenge(
                MakeChallenge(id: 7, EChallengeType.EnemiesKilled, goal: 3m), progress: 3m, completed: true,
                completedAt: DateTime.UtcNow);
            var inProgress = new PlayerChallenge(
                MakeChallenge(id: 9, EChallengeType.EnemiesKilled, goal: 5m), progress: 2m, completed: false);
            var progress = MakeProgress(challenges: [completed, inProgress]);

            var ids = progress.CompletedChallengeIds();

            Assert.Equal(7, Assert.Single(ids));
        }

        [Fact]
        public void CompletedChallengeIds_NoChallenges_ReturnsEmpty()
        {
            var progress = MakeProgress();

            Assert.Empty(progress.CompletedChallengeIds());
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static PlayerProgress MakeProgress(
            Player? player = null,
            IEnumerable<PlayerStatistic>? statistics = null,
            IEnumerable<PlayerChallenge>? challenges = null,
            IEnumerable<PlayerProficiency>? proficiencies = null)
        {
            return new PlayerProgress(player ?? new PlayerBuilder().Build(), statistics ?? [], challenges ?? [], proficiencies ?? []);
        }

        private static PlayerStatistic Stat(EStatisticType type, int? entityId, decimal value) =>
            new() { Type = type, EntityId = entityId, Value = value };

        private static PlayerProficiency Prof(int proficiencyId, int level, decimal xp) =>
            new() { ProficiencyId = proficiencyId, Level = level, Xp = xp };

        private static Challenge MakeChallenge(
            int id,
            EChallengeType type,
            decimal goal,
            int? targetEntityId = null,
            int? rewardItemId = null,
            int? rewardItemModId = null,
            DateTime? retiredAt = null) => new()
            {
                Id = id,
                Name = "Test Challenge",
                Description = string.Empty,
                DesignerNotes = string.Empty,
                Type = new ChallengeType(type),
                TargetEntityId = targetEntityId,
                ProgressGoal = goal,
                RewardItemId = rewardItemId,
                RewardItemModId = rewardItemModId,
                RetiredAt = retiredAt,
            };

        // Boss-ness is now driven by the explicit isBossBattle marker passed to RecordBattleCompleted,
        // not the enemy's IsBoss flag, so the test enemy is always built as a plain enemy.
        private static Enemy MakeEnemy(int id = 1) => new()
        {
            Id = id,
            Name = "Test Enemy",
            Level = 1,
            IsBoss = false,
            AttributeDistributions = [],
            AvailableSkills = [],
        };

    }
}
