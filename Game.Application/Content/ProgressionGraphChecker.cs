using Game.Core;
using Game.Core.Attributes;
using Contracts = Game.Abstractions.Contracts;

namespace Game.Application.Content
{
    /// <inheritdoc cref="IProgressionGraphChecker"/>
    internal sealed class ProgressionGraphChecker : IProgressionGraphChecker
    {
        public IReadOnlyList<ContentGraphFinding> Check(ContentGraph graph)
        {
            var run = new GraphRun(graph);
            run.Run();
            // Deterministic order so the CI report and any admin view are stable across runs.
            return run.Findings
                .OrderBy(f => f.Check, StringComparer.Ordinal)
                .ThenBy(f => f.EntityKind, StringComparer.Ordinal)
                .ThenBy(f => f.EntityId)
                .ThenBy(f => f.Message, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// A single lint pass. Only <b>live</b> (non-retired) records are checked as sources — a retired record
        /// is out of circulation, so a dangling reference it carries is harmless. References <i>to</i> a retired
        /// record are the interesting case: valid when authored, silently broken once the target is retired.
        /// </summary>
        private sealed class GraphRun
        {
            private readonly ContentGraph _graph;
            public readonly List<ContentGraphFinding> Findings = [];

            // id -> RetiredAt for each set (null value = live). Referenced-set membership + retirement in one map.
            private readonly Dictionary<int, DateTime?> _skillRetire;
            private readonly Dictionary<int, DateTime?> _itemRetire;
            private readonly Dictionary<int, DateTime?> _itemModRetire;
            private readonly Dictionary<int, DateTime?> _enemyRetire;
            private readonly Dictionary<int, DateTime?> _zoneRetire;
            private readonly Dictionary<int, DateTime?> _challengeRetire;
            private readonly Dictionary<int, DateTime?> _pathRetire;
            private readonly Dictionary<int, DateTime?> _proficiencyRetire;

            private readonly Dictionary<int, Contracts.Skill> _skills;
            private readonly Dictionary<int, Contracts.Enemy> _enemies;
            private readonly Dictionary<int, Contracts.Zone> _zones;
            private readonly Dictionary<int, Contracts.Challenge> _challenges;
            private readonly Dictionary<int, Contracts.Proficiency> _proficiencies;

            public GraphRun(ContentGraph graph)
            {
                _graph = graph;
                _skillRetire = RetireMap(graph.Skills, s => s.Id, s => s.RetiredAt);
                _itemRetire = RetireMap(graph.Items, i => i.Id, i => i.RetiredAt);
                _itemModRetire = RetireMap(graph.ItemMods, m => m.Id, m => m.RetiredAt);
                _enemyRetire = RetireMap(graph.Enemies, e => e.Id, e => e.RetiredAt);
                _zoneRetire = RetireMap(graph.Zones, z => z.Id, z => z.RetiredAt);
                _challengeRetire = RetireMap(graph.Challenges, c => c.Id, c => c.RetiredAt);
                _pathRetire = RetireMap(graph.Paths, p => p.Id, p => p.RetiredAt);
                _proficiencyRetire = RetireMap(graph.Proficiencies, p => p.Id, p => p.RetiredAt);

                _skills = ById(graph.Skills, s => s.Id);
                _enemies = ById(graph.Enemies, e => e.Id);
                _zones = ById(graph.Zones, z => z.Id);
                _challenges = ById(graph.Challenges, c => c.Id);
                _proficiencies = ById(graph.Proficiencies, p => p.Id);
            }

            public void Run()
            {
                CheckZones();
                CheckChallenges();
                CheckEnemies();
                CheckClasses();
                CheckItems();
                CheckProficiencies();
                CheckSkillRecipes();
                CheckOrphanSkills();
                CheckRecipeInputOwnability();
                CheckZoneReachability();
                CheckSingleHomeZone();
                CheckEmptyCombatZones();
            }

            // --- Zones ------------------------------------------------------------------------------------

            private void CheckZones()
            {
                foreach (var zone in Live(_graph.Zones, z => z.RetiredAt))
                {
                    if (zone.BossEnemyId is int bossId)
                    {
                        // A retired boss can no longer be challenged, stranding a live zone's boss action.
                        CheckRef("ZoneBoss", "Zone", zone.Id, _enemyRetire, "enemy", bossId, ContentGraphSeverity.Error, ContentGraphSeverity.Error);
                        if (_enemies.TryGetValue(bossId, out var boss) && boss.RetiredAt is null && !boss.IsBoss)
                        {
                            Warn("ZoneBoss", "Zone", zone.Id, $"boss slot references enemy {bossId}, which is not flagged as a boss.");
                        }
                    }

                    if (zone.UnlockChallengeId is int challengeId)
                    {
                        // A live zone gated by a retired/missing challenge can never be unlocked — a permanent lock.
                        CheckRef("ZoneUnlock", "Zone", zone.Id, _challengeRetire, "challenge", challengeId, ContentGraphSeverity.Error, ContentGraphSeverity.Error);
                    }
                }
            }

            // --- Challenges -------------------------------------------------------------------------------

            private void CheckChallenges()
            {
                foreach (var challenge in Live(_graph.Challenges, c => c.RetiredAt))
                {
                    // A KillsByDamageType challenge with no target can never track progress — the statistic
                    // writes only per-damage-type-key rows, no global row (#1455).
                    if (challenge.ChallengeTypeId == EChallengeType.KillsByDamageType && challenge.TargetEntityId is null)
                    {
                        Error("ChallengeTarget", "Challenge", challenge.Id, "is a KillsByDamageType challenge with no target damage-type key, so it can never track progress.");
                    }

                    // A challenge whose completion target is retired can never be completed (unreachable content);
                    // a missing target id is a dangling reference (a genuine break).
                    if (challenge.TargetEntityId is int targetId)
                    {
                        switch (challenge.EntityType)
                        {
                            case EEntityType.Enemy:
                                CheckRef("ChallengeTarget", "Challenge", challenge.Id, _enemyRetire, "enemy", targetId, ContentGraphSeverity.Error, ContentGraphSeverity.Warning);
                                break;
                            case EEntityType.Zone:
                                CheckRef("ChallengeTarget", "Challenge", challenge.Id, _zoneRetire, "zone", targetId, ContentGraphSeverity.Error, ContentGraphSeverity.Warning);
                                break;
                            case EEntityType.Skill:
                                CheckRef("ChallengeTarget", "Challenge", challenge.Id, _skillRetire, "skill", targetId, ContentGraphSeverity.Error, ContentGraphSeverity.Warning);
                                break;
                            case EEntityType.DamageType:
                                // Not a DB reference table — a fixed intrinsic enum, like Item.WeaponType — so
                                // validity is a structural enum-membership check, not a retirement lookup.
                                if (!Enum.IsDefined(typeof(EDamageTypeKey), targetId))
                                {
                                    Error("ChallengeTarget", "Challenge", challenge.Id, $"targets damage-type key {targetId}, which is not a defined EDamageTypeKey.");
                                }
                                break;
                            case EEntityType.None:
                                break;
                        }
                    }

                    // Rewards resolve by id forever, so retiring one is only a warning; a missing id is a break.
                    if (challenge.RewardItemId is int rewardItemId)
                    {
                        CheckRef("ChallengeReward", "Challenge", challenge.Id, _itemRetire, "item", rewardItemId, ContentGraphSeverity.Error, ContentGraphSeverity.Warning);
                    }

                    if (challenge.RewardItemModId is int rewardModId)
                    {
                        CheckRef("ChallengeReward", "Challenge", challenge.Id, _itemModRetire, "item mod", rewardModId, ContentGraphSeverity.Error, ContentGraphSeverity.Warning);
                    }
                }
            }

            // --- Enemies ----------------------------------------------------------------------------------

            private void CheckEnemies()
            {
                foreach (var enemy in Live(_graph.Enemies, e => e.RetiredAt))
                {
                    foreach (var skillId in enemy.SkillPool)
                    {
                        CheckRef("EnemySkill", "Enemy", enemy.Id, _skillRetire, "skill", skillId, ContentGraphSeverity.Error, ContentGraphSeverity.Warning);
                        if (_skills.TryGetValue(skillId, out var skill) && skill.RetiredAt is null && !HasFlag(skill, ESkillAcquisition.Enemy))
                        {
                            Warn("EnemySkill", "Enemy", enemy.Id, $"skill pool includes skill {skillId}, which is not Enemy-acquirable.");
                        }
                    }

                    foreach (var spawn in enemy.Spawns)
                    {
                        CheckRef("EnemySpawn", "Enemy", enemy.Id, _zoneRetire, "zone", spawn.ZoneId, ContentGraphSeverity.Error, ContentGraphSeverity.Warning);
                        if (_zones.TryGetValue(spawn.ZoneId, out var zone) && zone.RetiredAt is null && zone.IsHome)
                        {
                            Warn("EnemySpawn", "Enemy", enemy.Id, $"spawns in zone {spawn.ZoneId}, which is a no-combat Home zone.");
                        }
                    }
                }
            }

            // --- Classes ----------------------------------------------------------------------------------

            private void CheckClasses()
            {
                foreach (var cls in Live(_graph.Classes, c => c.RetiredAt))
                {
                    foreach (var skillId in cls.StarterSkillIds)
                    {
                        CheckRef("ClassStarterSkill", "Class", cls.Id, _skillRetire, "skill", skillId, ContentGraphSeverity.Error, ContentGraphSeverity.Warning);
                        if (_skills.TryGetValue(skillId, out var skill) && skill.RetiredAt is null && !HasFlag(skill, ESkillAcquisition.Player))
                        {
                            Warn("ClassStarterSkill", "Class", cls.Id, $"starter kit includes skill {skillId}, which is not Player-acquirable.");
                        }
                    }

                    foreach (var equipment in cls.StarterEquipment)
                    {
                        CheckRef("ClassStarterItem", "Class", cls.Id, _itemRetire, "item", equipment.ItemId, ContentGraphSeverity.Error, ContentGraphSeverity.Warning);
                    }
                }
            }

            // --- Items ------------------------------------------------------------------------------------

            private void CheckItems()
            {
                foreach (var item in Live(_graph.Items, i => i.RetiredAt))
                {
                    CheckWeaponStranding(item);

                    if (item.GrantedSkillId is int grantedSkillId)
                    {
                        CheckRef("ItemGrant", "Item", item.Id, _skillRetire, "skill", grantedSkillId, ContentGraphSeverity.Error, ContentGraphSeverity.Warning);
                        if (_skills.TryGetValue(grantedSkillId, out var skill) && skill.RetiredAt is null && !HasFlag(skill, ESkillAcquisition.Item))
                        {
                            Warn("ItemGrant", "Item", item.Id, $"grants skill {grantedSkillId}, which is not Item-acquirable.");
                        }

                        CheckWeaponSignatureType(item, grantedSkillId);
                    }

                    if (item.RequiredProficiencyId is int requiredProficiencyId)
                    {
                        CheckProficiencyGate(item, requiredProficiencyId);
                    }
                }
            }

            private void CheckWeaponStranding(Contracts.Item item)
            {
                if (item.ItemCategoryId == EItemCategory.Weapon)
                {
                    // Every weapon must declare a WeaponType (any damage-type leaf — a caster weapon declares
                    // its element) and bring a signature attack fieldable with it, or it strands the player
                    // once the weapon-match gate dims mismatched selected skills (the signature check is below).
                    if (item.WeaponType is not EDamageType weaponType || !Enum.IsDefined(weaponType))
                    {
                        Error("WeaponStranding", "Item", item.Id, "is a weapon but does not declare a valid WeaponType.");
                    }

                    if (item.GrantedSkillId is null)
                    {
                        Error("WeaponStranding", "Item", item.Id, "is a weapon but grants no signature skill (GrantedSkillId is null).");
                    }
                }
                else if (item.WeaponType is not null)
                {
                    Error("WeaponStranding", "Item", item.Id, "is not a weapon but declares a WeaponType.");
                }
            }

            private void CheckWeaponSignatureType(Contracts.Item item, int grantedSkillId)
            {
                // A weapon's signature must actually fire with that weapon. The weapon-match gate dims a
                // martial (weapon-leaf) skill whose type doesn't match the equipped weapon, so a weapon
                // granting a mismatched martial signature strands the player exactly as a missing signature
                // would; a non-martial signature (elemental/DoT/caster) is never gated and always qualifies —
                // and the type lives on the granted Skill record, out of a per-item save's reach.
                if (item.ItemCategoryId != EItemCategory.Weapon
                    || item.WeaponType is not EDamageType weaponType
                    || !_skills.TryGetValue(grantedSkillId, out var skill)
                    || skill.RetiredAt is not null)
                {
                    return;
                }

                var signatureType = PrimaryDamageType(skill);
                if (DamageTypes.IsWeaponLeaf(signatureType) && signatureType != weaponType)
                {
                    Error("WeaponStranding", "Item", item.Id, $"grants signature skill {grantedSkillId} of weapon type {signatureType}, which the equipped {weaponType} weapon dims — the weapon fields no usable signature.");
                }
            }

            // Mirrors Game.Core.Skills.Skill.PrimaryDamageType over the read contract: highest-weight portion,
            // first-authored on a tie, Physical for an empty split.
            private static EDamageType PrimaryDamageType(Contracts.Skill skill)
            {
                var portions = skill.DamagePortions.ToList();
                if (portions.Count == 0)
                {
                    return EDamageType.Physical;
                }

                var primary = portions[0];
                foreach (var portion in portions)
                {
                    if (portion.Weight > primary.Weight)
                    {
                        primary = portion;
                    }
                }
                return primary.Type;
            }

            private void CheckProficiencyGate(Contracts.Item item, int proficiencyId)
            {
                if (!_proficiencies.TryGetValue(proficiencyId, out var proficiency))
                {
                    Error("ItemProficiencyGate", "Item", item.Id, $"requires proficiency {proficiencyId}, which does not exist.");
                    return;
                }

                // A gated item can never be equipped if the gating proficiency is frozen (retired, or on a retired
                // path) or if the required level is beyond the proficiency's reach.
                if (proficiency.RetiredAt is not null || IsPathRetired(proficiency.PathId))
                {
                    Error("ItemProficiencyGate", "Item", item.Id, $"requires proficiency {proficiencyId}, which is retired/frozen, so it can never be equipped.");
                }

                if (item.RequiredProficiencyLevel > proficiency.MaxLevel)
                {
                    Error("ItemProficiencyGate", "Item", item.Id, $"requires proficiency {proficiencyId} at level {item.RequiredProficiencyLevel}, above its max level {proficiency.MaxLevel}.");
                }
            }

            // --- Proficiencies ----------------------------------------------------------------------------

            private void CheckProficiencies()
            {
                foreach (var proficiency in Live(_graph.Proficiencies, p => p.RetiredAt))
                {
                    // A proficiency on a nonexistent path is a structural break regardless of the path's state.
                    CheckRef("ProficiencyPath", "Proficiency", proficiency.Id, _pathRetire, "path", proficiency.PathId, ContentGraphSeverity.Error, retired: null);

                    // A proficiency frozen by a retired path accrues nothing and grants nothing further, so its
                    // reward/prerequisite intent is moot — skip the intent checks for it.
                    var onLivePath = !IsPathRetired(proficiency.PathId);

                    foreach (var reward in proficiency.LevelRewards)
                    {
                        if (onLivePath)
                        {
                            CheckRef("ProficiencyReward", "Proficiency", proficiency.Id, _skillRetire, "skill", reward.RewardSkillId, ContentGraphSeverity.Error, ContentGraphSeverity.Warning);
                            if (_skills.TryGetValue(reward.RewardSkillId, out var skill) && skill.RetiredAt is null && !HasFlag(skill, ESkillAcquisition.Player))
                            {
                                Warn("ProficiencyReward", "Proficiency", proficiency.Id, $"milestone grants skill {reward.RewardSkillId}, which is not Player-acquirable.");
                            }

                            if (reward.Level < 1 || reward.Level > proficiency.MaxLevel)
                            {
                                Warn("ProficiencyReward", "Proficiency", proficiency.Id, $"milestone at level {reward.Level} is outside the reachable range 1..{proficiency.MaxLevel}, so it can never be earned.");
                            }
                        }
                    }

                    foreach (var prerequisiteId in proficiency.PrerequisiteIds)
                    {
                        if (!_proficiencies.TryGetValue(prerequisiteId, out var prerequisite))
                        {
                            Error("ProficiencyPrerequisite", "Proficiency", proficiency.Id, $"has prerequisite proficiency {prerequisiteId}, which does not exist.");
                            continue;
                        }

                        // A gateway whose prerequisite is frozen can never open — the gated tier soft-locks.
                        if (onLivePath && (prerequisite.RetiredAt is not null || IsPathRetired(prerequisite.PathId)))
                        {
                            Error("ProficiencyPrerequisite", "Proficiency", proficiency.Id, $"has prerequisite proficiency {prerequisiteId}, which is retired/frozen, so this tier can never open.");
                        }
                    }
                }
            }

            // --- Skill recipes ----------------------------------------------------------------------------

            private void CheckSkillRecipes()
            {
                foreach (var recipe in Live(_graph.SkillRecipes, r => r.RetiredAt))
                {
                    CheckRef("RecipeResult", "SkillRecipe", recipe.Id, _skillRetire, "result skill", recipe.ResultSkillId, ContentGraphSeverity.Error, ContentGraphSeverity.Warning);
                    if (_skills.TryGetValue(recipe.ResultSkillId, out var result) && result.RetiredAt is null && !HasFlag(result, ESkillAcquisition.Synthesis))
                    {
                        Warn("RecipeResult", "SkillRecipe", recipe.Id, $"produces skill {recipe.ResultSkillId}, which is not Synthesis-acquirable.");
                    }

                    foreach (var inputSkillId in recipe.InputSkillIds)
                    {
                        CheckRef("RecipeInput", "SkillRecipe", recipe.Id, _skillRetire, "input skill", inputSkillId, ContentGraphSeverity.Error, ContentGraphSeverity.Warning);
                    }

                    foreach (var condition in recipe.Conditions)
                    {
                        if (!_proficiencies.TryGetValue(condition.ProficiencyId, out var proficiency))
                        {
                            Error("RecipeCondition", "SkillRecipe", recipe.Id, $"has a condition on proficiency {condition.ProficiencyId}, which does not exist.");
                            continue;
                        }

                        if (proficiency.RetiredAt is not null || IsPathRetired(proficiency.PathId))
                        {
                            Warn("RecipeCondition", "SkillRecipe", recipe.Id, $"has a condition on proficiency {condition.ProficiencyId}, which is retired/frozen.");
                        }

                        if (condition.MinLevel > proficiency.MaxLevel)
                        {
                            Warn("RecipeCondition", "SkillRecipe", recipe.Id, $"requires proficiency {condition.ProficiencyId} at level {condition.MinLevel}, above its max level {proficiency.MaxLevel}.");
                        }
                    }
                }
            }

            // --- Orphan skills (acquisition intent vs. graph reality) -------------------------------------

            private void CheckOrphanSkills()
            {
                var itemGranted = LiveSelectMany(_graph.Items, i => i.RetiredAt, i => i.GrantedSkillId is int id ? [id] : Array.Empty<int>());
                var synthesisProduced = LiveSelect(_graph.SkillRecipes, r => r.RetiredAt, r => r.ResultSkillId);
                var enemyAssigned = LiveSelectMany(_graph.Enemies, e => e.RetiredAt, e => e.SkillPool);
                var classGranted = LiveSelectMany(_graph.Classes, c => c.RetiredAt, c => c.StarterSkillIds);
                var milestoneGranted = LiveSelectMany(
                    _graph.Proficiencies.Where(p => p.RetiredAt is null && !IsPathRetired(p.PathId)),
                    p => p.RetiredAt,
                    p => p.LevelRewards.Select(r => r.RewardSkillId));

                foreach (var skill in Live(_graph.Skills, s => s.RetiredAt))
                {
                    if (HasFlag(skill, ESkillAcquisition.Item) && !itemGranted.Contains(skill.Id))
                    {
                        Warn("OrphanSkill", "Skill", skill.Id, "is Item-acquirable but no live item grants it.");
                    }

                    if (HasFlag(skill, ESkillAcquisition.Synthesis) && !synthesisProduced.Contains(skill.Id))
                    {
                        Warn("OrphanSkill", "Skill", skill.Id, "is Synthesis-acquirable but no live recipe produces it.");
                    }

                    if (HasFlag(skill, ESkillAcquisition.Enemy) && !enemyAssigned.Contains(skill.Id))
                    {
                        Warn("OrphanSkill", "Skill", skill.Id, "is Enemy-acquirable but no live enemy pool includes it.");
                    }

                    // Punch is the virtual-fists signature granted by the weapon system, not a kit/milestone.
                    if (HasFlag(skill, ESkillAcquisition.Player)
                        && skill.Id != GameConstants.PunchSkillId
                        && !classGranted.Contains(skill.Id)
                        && !milestoneGranted.Contains(skill.Id))
                    {
                        Warn("OrphanSkill", "Skill", skill.Id, "is Player-acquirable but no class kit or proficiency milestone grants it.");
                    }
                }
            }

            // --- Recipe input ownability (transitive reachability) ----------------------------------------

            private void CheckRecipeInputOwnability()
            {
                var liveRecipes = _graph.SkillRecipes.Where(r => r.RetiredAt is null).ToList();
                if (liveRecipes.Count == 0)
                {
                    return;
                }

                // A skill is ownable as an unlocked skill if a class kit or milestone grants it, or a recipe
                // whose inputs are all ownable produces it. Item-granted skills are innate (never in the player's
                // unlocked set), so they don't count toward synthesis-input ownability. Fixpoint to convergence.
                var ownable = new HashSet<int>(LiveSelectMany(_graph.Classes, c => c.RetiredAt, c => c.StarterSkillIds));
                ownable.UnionWith(LiveSelectMany(
                    _graph.Proficiencies.Where(p => p.RetiredAt is null && !IsPathRetired(p.PathId)),
                    p => p.RetiredAt,
                    p => p.LevelRewards.Select(r => r.RewardSkillId)));

                bool changed;
                do
                {
                    changed = false;
                    foreach (var recipe in liveRecipes)
                    {
                        if (!ownable.Contains(recipe.ResultSkillId) && recipe.InputSkillIds.All(ownable.Contains))
                        {
                            ownable.Add(recipe.ResultSkillId);
                            changed = true;
                        }
                    }
                }
                while (changed);

                foreach (var recipe in liveRecipes)
                {
                    foreach (var inputSkillId in recipe.InputSkillIds)
                    {
                        // Only flag inputs that resolve to a live skill (a dangling/retired input is reported by
                        // CheckSkillRecipes); the gap here is a real skill that has no acquisition path.
                        if (IsLiveSkill(inputSkillId) && !ownable.Contains(inputSkillId))
                        {
                            Warn("RecipeInputOwnable", "SkillRecipe", recipe.Id, $"input skill {inputSkillId} can never be owned (no class kit, milestone, or synthesis source grants it).");
                        }
                    }
                }
            }

            // --- Zone unlock reachability -----------------------------------------------------------------

            private void CheckZoneReachability()
            {
                var combatZones = _graph.Zones.Where(z => z.RetiredAt is null && !z.IsHome).ToList();
                if (combatZones.Count == 0)
                {
                    return;
                }

                var startZones = combatZones.Where(z => z.UnlockChallengeId is null).ToList();
                if (startZones.Count == 0)
                {
                    // No always-open zone means the player can never begin — the whole progression is stranded.
                    foreach (var zone in combatZones)
                    {
                        Warn("ZoneReachability", "Zone", zone.Id, "no zone is open from the start (every live zone is gated), so none is reachable.");
                    }
                    return;
                }

                var reachable = new HashSet<int>(startZones.Select(z => z.Id));
                bool changed;
                do
                {
                    changed = false;
                    foreach (var zone in combatZones)
                    {
                        if (!reachable.Contains(zone.Id) && IsZoneUnlockable(zone, reachable))
                        {
                            reachable.Add(zone.Id);
                            changed = true;
                        }
                    }
                }
                while (changed);

                foreach (var zone in combatZones.Where(z => !reachable.Contains(z.Id)))
                {
                    Warn("ZoneReachability", "Zone", zone.Id, "has no unlock path from a starting zone (a dead-end or cyclic unlock chain).");
                }
            }

            private bool IsZoneUnlockable(Contracts.Zone zone, HashSet<int> reachableZones)
            {
                if (zone.UnlockChallengeId is not int challengeId
                    || !_challenges.TryGetValue(challengeId, out var challenge)
                    || challenge.RetiredAt is not null)
                {
                    return false;
                }

                // The gating challenge must itself be achievable from already-reachable content. A zone- or
                // enemy-targeted challenge depends on that target being reachable; anything else (level, battles
                // won, a skill used) is achievable through general play once any zone is reachable.
                return challenge switch
                {
                    { EntityType: EEntityType.Zone, TargetEntityId: int zoneId } => reachableZones.Contains(zoneId),
                    { EntityType: EEntityType.Enemy, TargetEntityId: int enemyId } => IsEnemyReachable(enemyId, reachableZones),
                    _ => true,
                };
            }

            private bool IsEnemyReachable(int enemyId, HashSet<int> reachableZones)
            {
                if (!_enemies.TryGetValue(enemyId, out var enemy) || enemy.RetiredAt is not null)
                {
                    return false;
                }

                // Reachable if it spawns in a reachable zone, or is the dedicated boss of one.
                if (enemy.Spawns.Any(s => reachableZones.Contains(s.ZoneId)))
                {
                    return true;
                }

                return _graph.Zones.Any(z => z.RetiredAt is null && z.BossEnemyId == enemyId && reachableZones.Contains(z.Id));
            }

            // --- Single Home zone + empty combat zones ----------------------------------------------------

            private void CheckSingleHomeZone()
            {
                var homeZones = _graph.Zones.Where(z => z.RetiredAt is null && z.IsHome).ToList();
                if (homeZones.Count > 1)
                {
                    foreach (var zone in homeZones)
                    {
                        Error("SingleHomeZone", "Zone", zone.Id, $"is one of {homeZones.Count} live Home zones; the Home sanctuary must be singular.");
                    }
                }
            }

            private void CheckEmptyCombatZones()
            {
                var spawnZoneIds = LiveSelectMany(_graph.Enemies, e => e.RetiredAt, e => e.Spawns.Select(s => s.ZoneId));

                foreach (var zone in _graph.Zones.Where(z => z.RetiredAt is null && !z.IsHome))
                {
                    if (!spawnZoneIds.Contains(zone.Id))
                    {
                        Warn("EmptyCombatZone", "Zone", zone.Id, "is a live combat zone with no spawnable enemies; players are relocated out of it.");
                    }
                }
            }

            // --- Helpers ----------------------------------------------------------------------------------

            private bool IsPathRetired(int pathId) => !_pathRetire.TryGetValue(pathId, out var retiredAt) || retiredAt is not null;

            private bool IsLiveSkill(int skillId) => _skillRetire.TryGetValue(skillId, out var retiredAt) && retiredAt is null;

            private static bool HasFlag(Contracts.Skill skill, ESkillAcquisition flag) => (skill.Acquisition & flag) != 0;

            private void CheckRef(
                string check, string kind, int entityId,
                Dictionary<int, DateTime?> target, string targetLabel, int targetId,
                ContentGraphSeverity missing, ContentGraphSeverity? retired)
            {
                if (!target.TryGetValue(targetId, out var retiredAt))
                {
                    Findings.Add(new(missing, check, kind, entityId, $"references {targetLabel} {targetId}, which does not exist."));
                }
                else if (retiredAt is not null && retired is ContentGraphSeverity severity)
                {
                    Findings.Add(new(severity, check, kind, entityId, $"references {targetLabel} {targetId}, which is retired."));
                }
            }

            private void Warn(string check, string kind, int entityId, string message)
                => Findings.Add(new(ContentGraphSeverity.Warning, check, kind, entityId, message));

            private void Error(string check, string kind, int entityId, string message)
                => Findings.Add(new(ContentGraphSeverity.Error, check, kind, entityId, message));

            private static IEnumerable<T> Live<T>(IEnumerable<T> items, Func<T, DateTime?> retiredAt)
                => items.Where(i => retiredAt(i) is null);

            private static HashSet<int> LiveSelect<T>(IEnumerable<T> items, Func<T, DateTime?> retiredAt, Func<T, int> select)
                => Live(items, retiredAt).Select(select).ToHashSet();

            private static HashSet<int> LiveSelectMany<T>(IEnumerable<T> items, Func<T, DateTime?> retiredAt, Func<T, IEnumerable<int>> select)
                => Live(items, retiredAt).SelectMany(select).ToHashSet();

            private static Dictionary<int, DateTime?> RetireMap<T>(IEnumerable<T> items, Func<T, int> id, Func<T, DateTime?> retiredAt)
                => items.ToDictionary(id, retiredAt);

            private static Dictionary<int, T> ById<T>(IEnumerable<T> items, Func<T, int> id)
                => items.ToDictionary(id);
        }
    }
}
