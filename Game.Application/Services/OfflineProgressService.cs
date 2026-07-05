using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Core.Battle;
using Game.Core.Battle.Offline;
using Game.Core.Players;
using Game.Core.Proficiencies;
using Game.Core.Progress;
using CoreClass = Game.Core.Classes.Class;
using CoreEnemy = Game.Core.Enemies.Enemy;
using CoreZone = Game.Core.Zones.Zone;

namespace Game.Application.Services
{
    /// <summary>
    /// Computes and applies a returning (or switched-away) player's offline progress: replays the missed
    /// idle/boss battles over the away window (spike #879) and folds the accumulated rewards — exp, levels,
    /// stat points, completed challenges, and proficiency gains — onto the player and progress aggregates.
    /// Extracted from <see cref="BattleService"/> (#1516), which every offline/consolidation feature was landing
    /// in; it touches the live battle lifecycle only through <see cref="BattleService.ResolveStaleBattle"/>,
    /// settling a disconnected in-flight battle before the away window replays.
    /// </summary>
    public class OfflineProgressService(
        IPlayerRepository playerRepo,
        IPlayerProgressRepository progressRepo,
        IItems items,
        IItemMods itemMods,
        ISkills skills,
        IProficiencies proficiencies,
        IClasses classes,
        IEnemies enemies,
        OfflineProgressSimulator offlineSimulator,
        ChallengeRewardService challengeRewards,
        ProficiencyRewardService proficiencyRewards,
        ZoneResolutionService zoneResolution,
        BattleService battleService)
    {
        private readonly IPlayerRepository _playerRepo = playerRepo;
        private readonly IPlayerProgressRepository _progressRepo = progressRepo;
        private readonly IItems _items = items;
        private readonly IItemMods _itemMods = itemMods;
        private readonly ISkills _skills = skills;
        private readonly IProficiencies _proficiencies = proficiencies;
        private readonly IClasses _classes = classes;
        private readonly IEnemies _enemies = enemies;
        private readonly OfflineProgressSimulator _offlineSimulator = offlineSimulator;
        private readonly ChallengeRewardService _challengeRewards = challengeRewards;
        private readonly ProficiencyRewardService _proficiencyRewards = proficiencyRewards;
        private readonly ZoneResolutionService _zoneResolution = zoneResolution;
        private readonly BattleService _battleService = battleService;

        // Offline-rewards window bounds (spike #879). Below the minimum, a return is treated as no time away
        // (no rewards, just re-anchor); above the cap, only the cap is ever simulated so a long absence (or an
        // all-draw zone) can't run unbounded CPU.
        public static readonly TimeSpan MinimumOfflineAway = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan MaximumOfflineSimulation = TimeSpan.FromHours(10);

        // CPU-waste guard handed to the offline simulator: an opening batch of pure draws is a stalemate the
        // player can neither win nor lose, so simulating the whole away budget of maximum-duration draws earns
        // nothing for the most work. Stop after this many opening all-draw battles (a true stalemate draws
        // every time, so a small count is decisive; any win or loss disables the guard).
        private const int StalemateCutoffBattles = 10;

        /// <summary>
        /// Computes how long the player was away, replays the missed idle/boss battles (spike #879), applies
        /// the accumulated rewards, and returns a welcome-back summary. Runs inline within the calling socket
        /// command (the cap keeps the worst case well under the command timeout); the command's cancellation
        /// token is threaded into the simulation loop so a long run unwinds promptly.
        /// <para>
        /// Away time is <c>now − <see cref="Player.LastActivity"/></c>, measured server-side. Below
        /// <see cref="MinimumOfflineAway"/> it is a no-op beyond re-anchoring <c>LastActivity</c> (so a second
        /// immediate call earns nothing); otherwise it resolves any stale in-flight battle, resumes the
        /// persisted loop mode in the player's (viability-checked) current zone, simulates up to
        /// <see cref="MaximumOfflineSimulation"/>, applies exp per victory and the consolidated
        /// statistics/challenges, re-anchors <c>LastActivity</c>, and persists once.
        /// </para>
        /// </summary>
        public Task<OfflineProgressSummary> SimulateOfflineProgress(Player player, PlayerState state, CancellationToken cancellationToken = default)
        {
            return SimulateProgress(player, state, MinimumOfflineAway, cancellationToken);
        }

        /// <summary>
        /// Credits a character being switched away from in a deliberate in-game character switch (spike #922):
        /// the same elapsed-time replay as <see cref="SimulateOfflineProgress"/> but with the
        /// <see cref="MinimumOfflineAway"/> floor dropped, so any elapsed time since the departed character's
        /// <see cref="Player.LastActivity"/> is credited (the 5-minute floor is a login-time concern). When the
        /// in-flight battle has settled (or reached the draw cap) it resolves the battle, applies the rewards,
        /// re-anchors <c>LastActivity</c>, and persists — so the departed character loses no idle progress; when
        /// that battle is still genuinely in progress it is instead handed back untouched (no rewards, no
        /// <c>LastActivity</c> re-anchor, no persist), leaving the away clock running against the original departure.
        /// The caller must invoke this off the departed character's battle loop (its socket torn down first),
        /// so the player-state write cannot race a live battle-completion command.
        /// </summary>
        public Task<OfflineProgressSummary> SimulateSwitchProgress(Player player, PlayerState state, CancellationToken cancellationToken = default)
        {
            return SimulateProgress(player, state, TimeSpan.Zero, cancellationToken);
        }

        // Shared elapsed-time replay for both the login welcome-back path and the deliberate-switch credit. The
        // only difference is the away floor: the login path skips a sub-5-minute return, while a switch credits
        // any elapsed time (minimumAway == zero), so the two cannot otherwise drift.
        private async Task<OfflineProgressSummary> SimulateProgress(Player player, PlayerState state, TimeSpan minimumAway, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var awayMs = (long)(now - player.LastActivity).TotalMilliseconds;
            var cappedAwayMs = Math.Min(awayMs, (long)MaximumOfflineSimulation.TotalMilliseconds);

            // Below the threshold there are no offline rewards. Re-anchor LastActivity (so the next away period
            // starts fresh and an immediate re-claim is a no-op) and return an empty summary. Any stale
            // in-flight battle is left for the idle loop's first StartBattle to abandon, exactly as on a normal
            // reconnect — there is no away window to simulate, so settling it here would change nothing.
            if (awayMs < minimumAway.TotalMilliseconds)
            {
                player.StampActivity(now);
                await _playerRepo.SavePlayer(player, cancellationToken);
                return OfflineProgressSummary.Empty(cappedAwayMs, player.AutoChallengeBoss, player.CurrentZoneId);
            }

            // Settle the disconnected battle before simulating the away window, so its outcome is credited once
            // (here) rather than being re-abandoned by the first live StartBattle after the gate. When the
            // battle is instead handed back still in progress (#1595) there is no settled outcome and no
            // leftover budget beyond it to spend on new battles — hand it back as-is rather than crediting a
            // window that hasn't actually elapsed for this fight. LastActivity is deliberately left untouched
            // (nothing was persisted), so the away clock keeps counting against the original disconnect until
            // real server time actually resolves the fight.
            if (await _battleService.ResolveStaleBattle(player, state, cancellationToken) is { } handoff)
            {
                return OfflineProgressSummary.StillInProgress(handoff, cappedAwayMs, player.AutoChallengeBoss, player.CurrentZoneId);
            }

            // Load the progress aggregate once for the whole offline pass. Its completed challenges (loop/zone
            // gating), proficiency levels (battle snapshot), and statistics (reward application) all read the
            // same Progress_{playerId} cache key, so threading one aggregate down replaces 3-4 serial round-trips.
            // Loaded after ResolveStaleBattle so it reflects the settled stale battle's progress mutation.
            var progress = await _progressRepo.Load(player, cancellationToken);
            var completedChallengeIds = progress.CompletedChallengeIds();

            var (mode, zone) = await ResolveOfflineLoop(player, completedChallengeIds, cancellationToken);
            var proficiencyLevels = ToProficiencyLevels(progress.Proficiencies);
            var parameters = BuildSimulationParameters(player, mode, zone, awayMs, proficiencyLevels);

            var result = _offlineSimulator.Simulate(parameters, cancellationToken);

            var levelBefore = player.Level;
            var statPointsBefore = player.StatPoints.StatPointsGained;
            var rewards = await ApplyOfflineRewards(player, progress, result, cancellationToken);

            // Re-anchor the away clock and persist the player (exp/levels/unlocks) in one save. The exp batch
            // already raised its own single core update in ApplyOfflineRewards; this re-anchor raises one more.
            // Both are absolute write-behind writes (the final state persists regardless), and two is still
            // nowhere near the per-victory flood decision 6 avoids.
            player.StampActivity(now);
            await _playerRepo.SavePlayer(player, cancellationToken);

            return new OfflineProgressSummary
            {
                AwayMs = cappedAwayMs,
                AutoChallengeBoss = result.IsBossBattle,
                ZoneId = result.ZoneId,
                BattlesWon = result.Wins,
                BattlesLost = result.Losses,
                BattlesDrawn = result.Draws,
                TotalExp = result.TotalExp,
                LevelsGained = player.Level - levelBefore,
                StatPointsGained = player.StatPoints.StatPointsGained - statPointsBefore,
                CompletedChallenges = rewards.CompletedChallenges,
                ProficiencyGains = rewards.ProficiencyGains.Results,
                OpenedProficiencies = rewards.ProficiencyGains.Opened,
            };
        }

        // Resolves which loop the offline simulation resumes and the zone it runs in. Boss mode resumes only
        // when the persisted flag is set and the current zone still has a challengeable boss (in circulation,
        // unlocked, boss authored); otherwise the loop falls back to idle in the nearest viable zone (the same
        // lazy-relocation the live idle loop uses, so a retired/empty current zone never stalls the sim).
        private async Task<(OfflineLoopMode Mode, CoreZone Zone)> ResolveOfflineLoop(
            Player player, IReadOnlySet<int> completedChallengeIds, CancellationToken cancellationToken)
        {
            var currentZoneId = player.CurrentZoneId;
            if (player.AutoChallengeBoss
                && _zoneResolution.ResolveChallengeableBossZone(currentZoneId, completedChallengeIds) is { } bossZone)
            {
                return (OfflineLoopMode.Boss, bossZone);
            }

            var idleZoneId = await _zoneResolution.EnsureViableZone(player, currentZoneId, completedChallengeIds, cancellationToken);
            return (OfflineLoopMode.Idle, _zoneResolution.GetDomainZone(idleZoneId));
        }

        // Builds the simulator inputs for the resolved loop. Idle rolls a random per-zone spawn each battle;
        // boss builds the zone's dedicated boss deterministically — the same factory resolvers the live battle
        // start uses, so an offline battle is constructed identically to an online one. The player snapshot and
        // the catalog resolvers are shared across the whole run; the simulator grows the snapshot's level
        // in-loop as victories are earned (#1601), so the player's power is not stationary across the window.
        private OfflineSimulationParameters BuildSimulationParameters(
            Player player, OfflineLoopMode mode, CoreZone zone, long awayMs,
            IReadOnlyList<ProficiencyLevelSnapshot> proficiencyLevels)
        {
            Func<int, CoreEnemy> resolveEnemy = mode == OfflineLoopMode.Boss
                ? _zoneResolution.BossEnemyResolver(zone)
                : level => _enemies.GetRandomDomainEnemy(zone.Id, level);

            return new OfflineSimulationParameters
            {
                // The stat allocations, gear, and proficiency levels this snapshot captures are frozen at the
                // window start (player-action state, #1601) — but the level it also captures grows in-loop as
                // the simulated victories earn exp, alongside StartingExp below.
                Snapshot = BattleSnapshot.FromPlayer(player, proficiencyLevels),
                StartingExp = player.Exp,
                Mode = mode,
                Zone = zone,
                AwayBudgetMs = awayMs,
                CapMs = (long)MaximumOfflineSimulation.TotalMilliseconds,
                CooldownMs = (int)BattleService.PostBattleCooldown.TotalMilliseconds,
                ResolveEnemy = resolveEnemy,
                ResolveItem = _items.GetItem,
                ResolveMod = _itemMods.GetItemMod,
                ResolveSkill = _skills.TryGetSkill,
                ResolveProficiency = _proficiencies.GetProficiency,
                ResolveClass = ResolveClass,
                SeedSource = BattleService.CreateBattleSeed,
                StalemateCutoffBattles = StalemateCutoffBattles,
            };
        }

        // Applies a simulated away window's rewards to the player and progress in one consolidated pass
        // (spike #879 decisions 6 & 7): each battle feeds the same per-battle statistics path the live handler
        // uses, exp is granted per victory (so the per-grant clamp never truncates a haul and levels accrue),
        // and the affected challenges are evaluated once at the end with the live per-challenge push suppressed
        // (the summary is the notification). Returns the completed challenges and the folded proficiency gains
        // (spike #982 decision 9 — the offline accrual's notification rides the summary, not a per-battle push).
        private async Task<OfflineRewards> ApplyOfflineRewards(
            Player player, PlayerProgress progress, OfflineProgressResult result, CancellationToken cancellationToken)
        {
            if (result.BattlesSimulated == 0)
            {
                return OfflineRewards.Empty;
            }

            var victoryExpRewards = new List<int>();
            var proficiencyGains = new ProficiencyGainAccumulator();
            // Union the statistic rows touched across every battle, so the end-of-window challenge evaluation
            // sees every moved statistic. RecordBattleCompleted currently returns the progress aggregate's
            // cumulative dirty set (it is loaded once for the whole window), but unioning each call's result
            // explicitly keeps this correct even if that method were ever changed to return only the per-battle
            // delta — a mixed-outcome window can touch a tracked statistic in a non-final battle (e.g. a kill
            // challenge crossed by early wins before a closing loss), and the union captures it regardless.
            var touchedStatistics = new HashSet<(EStatisticType Type, int? EntityId)>();
            foreach (var battle in result.Battles)
            {
                foreach (var key in progress.RecordBattleCompleted(
                    battle.Enemy, battle.Result.Victory, battle.Result.PlayerDied, battle.Result.TotalMs,
                    battle.Result.Stats, result.IsBossBattle, result.ZoneId))
                {
                    touchedStatistics.Add(key);
                }

                if (battle.Result.Victory)
                {
                    victoryExpRewards.Add(battle.ExpReward);

                    // Accrue proficiency XP per won battle, exactly as the live handler does — same inputs
                    // (this battle's skill stats + combat ratings), same service — so the offline accrual
                    // matches what the player would have earned live (the "offline == live" invariant). The push
                    // is suppressed; the folded results ride the welcome-back summary instead.
                    var ratingDenominator = Math.Max(battle.PlayerRating, battle.EnemyRating);
                    proficiencyGains.Add(_proficiencyRewards.AccrueAndApply(
                        progress, battle.Result.Stats, ratingDenominator, player, notify: false));
                }
            }

            // Grant the whole window's exp before evaluating challenges, so a statistic-independent
            // LevelReached challenge sees the post-window level (mirroring the live order, where exp is granted
            // before the battle-completed handler evaluates challenges).
            if (victoryExpRewards.Count > 0)
            {
                player.GrantOfflineExp(victoryExpRewards);
            }

            var completed = _challengeRewards.EvaluateAndApply(progress, touchedStatistics, player, notify: false);

            await _progressRepo.Save(progress, cancellationToken);
            return new OfflineRewards(completed, proficiencyGains.Build());
        }

        // Resolves the player's class for the locked-base distribution, failing loudly on an unresolvable id
        // (a content-data mistake) rather than silently dropping the attribute fingerprint — the player-load
        // missing-reference policy applied to the battle-assembly path. Duplicated (not shared) from
        // BattleService's identical live-path resolver: both are three-line, dependency-only helpers over each
        // class's own IClasses, not worth a shared abstraction (CLAUDE.md).
        private CoreClass ResolveClass(int classId) =>
            _classes.GetClass(classId)
            ?? throw new InvalidOperationException($"Class {classId} could not be resolved from the catalogue.");

        // Projects proficiency progress to the battle-snapshot's level-only view, deriving it from the progress
        // aggregate already loaded for this offline pass. Duplicated (not shared) from BattleService's identical
        // live-path projection (BattleService.CaptureProficiencyLevels reads through the lean accessor instead):
        // like ResolveClass above, a three-line, dependency-only helper not worth a shared abstraction (CLAUDE.md).
        private static List<ProficiencyLevelSnapshot> ToProficiencyLevels(IEnumerable<PlayerProficiency> proficiencies) =>
            proficiencies
                .Select(p => new ProficiencyLevelSnapshot { ProficiencyId = p.ProficiencyId, Level = p.Level })
                .ToList();
    }

    /// <summary>
    /// The welcome-back summary of a returning player's offline progress: how long they were away (capped),
    /// which loop ran, the battle tally, and the rewards earned (exp, levels, stat points, and the challenges
    /// completed with what they unlocked). Returned by <see cref="OfflineProgressService.SimulateOfflineProgress"/>
    /// and projected to the API model the client gate renders.
    /// </summary>
    public class OfflineProgressSummary
    {
        /// <summary>How long the player was away, in milliseconds, clamped to the simulation cap.</summary>
        public required long AwayMs { get; init; }

        /// <summary>Whether the simulated loop was auto-challenging the boss (<c>true</c>) or idle-farming
        /// (<c>false</c>).</summary>
        public required bool AutoChallengeBoss { get; init; }

        /// <summary>The zone the loop ran in.</summary>
        public required int ZoneId { get; init; }

        public required int BattlesWon { get; init; }
        public required int BattlesLost { get; init; }
        public required int BattlesDrawn { get; init; }

        /// <summary>Total experience earned across all victories.</summary>
        public required long TotalExp { get; init; }

        /// <summary>Levels gained over the window.</summary>
        public required int LevelsGained { get; init; }

        /// <summary>Stat points gained over the window (from the levels gained).</summary>
        public required int StatPointsGained { get; init; }

        /// <summary>The challenges completed over the window, each with the reward ids it unlocked.</summary>
        public required IReadOnlyList<CompletedChallenge> CompletedChallenges { get; init; }

        /// <summary>The proficiency gains accrued over the window, folded across every won battle: each trained
        /// proficiency's total XP gained, its final level/residual XP, the milestones it crossed, and the reward
        /// skills granted (spike #982 decision 9 — the offline accrual's notification rides this summary).</summary>
        public required IReadOnlyList<ProficiencyXpResult> ProficiencyGains { get; init; }

        /// <summary>The proficiency nodes opened over the window (a maxed tier's next tier or a newly-satisfied
        /// gateway), each with the seed skill it granted (if any).</summary>
        public required IReadOnlyList<ProficiencyOpened> OpenedProficiencies { get; init; }

        /// <summary>
        /// Non-null when the player's pre-existing battle was still genuinely in progress rather than concluded
        /// (#1595): the still-active battle's enemy/seed and elapsed offset the client must resume from
        /// (replay-to-offset, #1597). When set, no away-window battles were simulated — there was no leftover
        /// budget beyond this unconcluded fight — and every other field is at its default/empty value.
        /// </summary>
        public BattleStartResult? ActiveBattle { get; init; }

        /// <summary>
        /// Whether the window produced anything worth gating on. The frontend skips the welcome-back gate for
        /// an empty summary (a sub-threshold absence, or one that earned nothing) and enters the game directly.
        /// A window that only advanced proficiencies (e.g. a maxed-XP-level character) still reports progress.
        /// </summary>
        public bool HasProgress =>
            BattlesWon > 0 || BattlesLost > 0 || BattlesDrawn > 0
            || CompletedChallenges.Count > 0 || ProficiencyGains.Count > 0 || OpenedProficiencies.Count > 0
            || ActiveBattle is not null;

        /// <summary>An empty summary: away time recorded but nothing simulated (a sub-threshold return).</summary>
        public static OfflineProgressSummary Empty(long awayMs, bool autoChallengeBoss, int zoneId) => new()
        {
            AwayMs = awayMs,
            AutoChallengeBoss = autoChallengeBoss,
            ZoneId = zoneId,
            BattlesWon = 0,
            BattlesLost = 0,
            BattlesDrawn = 0,
            TotalExp = 0,
            LevelsGained = 0,
            StatPointsGained = 0,
            CompletedChallenges = [],
            ProficiencyGains = [],
            OpenedProficiencies = [],
        };

        /// <summary>A summary for a still-in-progress hand-back (#1595): no battles were simulated, only the
        /// active battle to resume is carried.</summary>
        public static OfflineProgressSummary StillInProgress(
            BattleStartResult activeBattle, long awayMs, bool autoChallengeBoss, int zoneId) => new()
            {
                AwayMs = awayMs,
                AutoChallengeBoss = autoChallengeBoss,
                ZoneId = zoneId,
                BattlesWon = 0,
                BattlesLost = 0,
                BattlesDrawn = 0,
                TotalExp = 0,
                LevelsGained = 0,
                StatPointsGained = 0,
                CompletedChallenges = [],
                ProficiencyGains = [],
                OpenedProficiencies = [],
                ActiveBattle = activeBattle,
            };
    }

    /// <summary>
    /// The rewards a simulated away window applied, returned by <see cref="OfflineProgressService"/>'s
    /// offline-rewards pass: the challenges completed and the folded proficiency gains (XP/levels/milestones/
    /// skills) plus opened nodes. Both feed the welcome-back summary; the per-challenge and per-battle live
    /// pushes are suppressed.
    /// </summary>
    public record OfflineRewards(
        IReadOnlyList<CompletedChallenge> CompletedChallenges,
        ProficiencyAccrualResult ProficiencyGains)
    {
        /// <summary>No rewards: nothing was simulated in the window.</summary>
        public static OfflineRewards Empty { get; } = new([], ProficiencyAccrualResult.Empty);
    }
}
