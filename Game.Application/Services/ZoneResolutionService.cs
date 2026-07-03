using Game.Abstractions.DataAccess;
using Game.Core.Players;
using CoreEnemy = Game.Core.Enemies.Enemy;
using CoreZone = Game.Core.Zones.Zone;

namespace Game.Application.Services
{
    /// <summary>
    /// Resolves which zone (and, for a boss loop, which enemy) a battle actually runs in: viability (in
    /// circulation, has a spawnable enemy), lazy relocation off a no-longer-viable zone, unlock checks, and the
    /// challengeable-boss gate plus its dedicated enemy resolver. Shared by <see cref="BattleService"/>'s live
    /// paths (StartBattle, StartBossBattle, SetAutoChallengeBoss) and <see cref="OfflineProgressService"/>'s
    /// away-window replay, so the "what zone/enemy does the loop resolve to" rule lives in one place for both
    /// rather than being duplicated across the live and offline orchestrators (#1516).
    /// </summary>
    public class ZoneResolutionService(IZones zones, IEnemies enemies, IPlayerRepository playerRepo, IPlayerProgressRepository progressRepo)
    {
        private readonly IZones _zones = zones;
        private readonly IEnemies _enemies = enemies;
        private readonly IPlayerRepository _playerRepo = playerRepo;
        private readonly IPlayerProgressRepository _progressRepo = progressRepo;

        /// <summary>Resolves the domain zone for an id already known to be in range (the caller's own gate, or
        /// a range check upstream) — a thin passthrough so callers need not hold their own <see cref="IZones"/>
        /// reference just to turn a resolved zone id back into the domain model.</summary>
        public CoreZone GetDomainZone(int zoneId) => _zones.GetDomainZone(zoneId);

        // Whether a zone is viable for the idle loop: in circulation (not retired) and carrying at least one
        // spawnable enemy. The two facts live in separate caches (zone retirement vs the enemy spawn tables),
        // so they are combined here rather than on either lean domain model. The id is range-checked first so
        // a stale/out-of-range CurrentZoneId reads as non-viable (and triggers relocation) instead of throwing.
        private bool IsZoneViable(int zoneId)
        {
            return _zones.ValidateZoneId(zoneId)
                && !_zones.IsZoneRetired(zoneId)
                && _enemies.HasSpawnableEnemies(zoneId);
        }

        /// <summary>
        /// Live-path overload: defers reading the completed-challenge set until a relocation is actually needed
        /// (the viability check short-circuits first), so the idle hot path never pays the read.
        /// </summary>
        public async Task<int> EnsureViableZone(Player player, int zoneId, CancellationToken cancellationToken)
        {
            // An out-of-range zone id is corruption/tampering, not a relocation case: leave it for the
            // downstream GetDomainZone to surface loudly (fail-fast) rather than silently relocating.
            if (!_zones.ValidateZoneId(zoneId) || IsZoneViable(zoneId))
            {
                return zoneId;
            }

            var completedChallengeIds = await _progressRepo.GetCompletedChallengeIds(player.Id, cancellationToken);
            return await EnsureViableZone(player, zoneId, completedChallengeIds, cancellationToken);
        }

        /// <summary>
        /// Relocates the player when their resolved zone is no longer viable, returning the zone the battle
        /// should run in. "Nearest" is the lowest-Order zone the player has unlocked that is viable, falling
        /// back to the starting zone. A no-op (no save) when the current zone is already viable. Takes the
        /// completed-challenge set so a caller holding a loaded progress aggregate (the offline pass) gates
        /// without re-reading the progress cache key.
        /// </summary>
        public async Task<int> EnsureViableZone(
            Player player, int zoneId, IReadOnlySet<int> completedChallengeIds, CancellationToken cancellationToken)
        {
            if (!_zones.ValidateZoneId(zoneId) || IsZoneViable(zoneId))
            {
                return zoneId;
            }

            // Filter to viable candidates before ordering, then resolve each domain zone once (lazily, so the
            // resolve stops at the first unlocked match) rather than re-resolving it inside the predicate.
            var destination = _zones.All()
                .Where(zone => IsZoneViable(zone.Id))
                .OrderBy(zone => zone.Order)
                .Select(zone => _zones.GetDomainZone(zone.Id))
                .FirstOrDefault(zone => zone.IsUnlocked(completedChallengeIds));
            var newZoneId = destination?.Id ?? NewPlayerFactory.StartingZoneId;

            if (newZoneId != player.CurrentZoneId)
            {
                player.ChangeZone(newZoneId);
                await _playerRepo.SavePlayer(player, cancellationToken);
            }

            return newZoneId;
        }

        // Shared challengeable-boss gate (StartBossBattle / SetAutoChallengeBoss / the offline boss loop):
        // resolves the zone iff its boss can actually be challenged — the id is in range (checked before
        // GetDomainZone, which throws on an out-of-range id), the zone is in circulation, and a dedicated boss
        // is authored. The unlock check differs per caller, so it lives on the two overloads below.
        private CoreZone? ResolveBossZone(int zoneId)
        {
            if (!_zones.ValidateZoneId(zoneId) || _zones.IsZoneRetired(zoneId))
            {
                return null;
            }

            var zone = _zones.GetDomainZone(zoneId);
            return zone.BossEnemyId is null ? null : zone;
        }

        /// <summary>
        /// Live-path gate: the unlock check reads the player's completed challenges through <see cref="IsZoneUnlocked"/>,
        /// incurred only once the cheaper range/retired/boss checks have passed.
        /// </summary>
        public async Task<CoreZone?> ResolveChallengeableBossZone(int playerId, int zoneId, CancellationToken cancellationToken)
        {
            var zone = ResolveBossZone(zoneId);
            if (zone is null || !await IsZoneUnlocked(playerId, zone, cancellationToken))
            {
                return null;
            }

            return zone;
        }

        /// <summary>
        /// Offline-pass gate: unlocks against a completed-challenge set the caller already holds (the loaded
        /// progress aggregate), so the check re-reads no progress cache key.
        /// </summary>
        public CoreZone? ResolveChallengeableBossZone(int zoneId, IReadOnlySet<int> completedChallengeIds)
        {
            var zone = ResolveBossZone(zoneId);
            return zone is not null && zone.IsUnlocked(completedChallengeIds) ? zone : null;
        }

        /// <summary>
        /// Whether a zone is unlocked for the player. An ungated zone is always open and pays no read cost;
        /// a gated zone costs one indexed completion lookup, incurred only on a real zone transition or a
        /// boss challenge (not per idle tick). The unlock rule itself lives on the domain Zone.
        /// </summary>
        public async Task<bool> IsZoneUnlocked(int playerId, CoreZone zone, CancellationToken cancellationToken = default)
        {
            if (zone.UnlockChallengeId is null)
            {
                return true;
            }

            var completedChallengeIds = await _progressRepo.GetCompletedChallengeIds(playerId, cancellationToken);
            return zone.IsUnlocked(completedChallengeIds);
        }

        // The dedicated-boss resolver shared by the live boss challenge and the offline boss loop: the zone's
        // authored boss at the requested (fixed boss) level. Callers only reach this for a zone the
        // challengeable-boss gate resolved (BossEnemyId set), so the null check here is a defensive invariant
        // rather than a reachable state.
        public Func<int, CoreEnemy> BossEnemyResolver(CoreZone zone)
        {
            var bossEnemyId = zone.BossEnemyId
                ?? throw new InvalidOperationException($"Zone {zone.Id} has no dedicated boss enemy authored.");
            return level => _enemies.GetDomainEnemy(bossEnemyId, level)
                ?? throw new InvalidOperationException(
                    $"Zone {zone.Id} references boss enemy {bossEnemyId}, which does not exist.");
        }
    }
}
