using Game.Core.Attributes;
using Game.Core.Battle;
using static Game.Core.Proficiencies.ProficiencyXpCalculator;

namespace Game.Core.Proficiencies
{
    /// <summary>
    /// The catalog resolvers <see cref="ProficiencyAccrual.Accrue"/> needs, bundled like
    /// <see cref="Battle.Offline.OfflineSimulationParameters"/>'s resolvers so the domain math stays independent
    /// of <c>IProficiencies</c> (#1602) — both the live application-layer adapter and the offline simulator
    /// build one from their own catalog access.
    /// </summary>
    public readonly record struct ProficiencyCatalog(
        Func<int, Proficiency> ResolveProficiency,
        Func<int, Path> ResolvePath,
        Func<EActivityKey, IReadOnlyList<Path>> PathsForActivityKey,
        Func<int, IReadOnlyList<int>> DependentsOf);

    /// <summary>
    /// The shared proficiency-XP accrual step for a won battle (effect-based model, spike #1318, max-normalized
    /// per spike #1526 Decision 5). Extracted from the application layer's <c>ProficiencyRewardService</c> into
    /// <c>Game.Core</c> (#1602) so the offline simulator can run it <em>in-loop</em>, immediately feeding a
    /// mid-window level-up's attribute payout into the next simulated battle's battler assembly — the live
    /// per-battle path already got this for free from the socket loop's natural sequencing.
    /// <para>
    /// Given the battle's skill stats and the combatants' combat ratings, it sums each path's activity for its
    /// <see cref="EActivityKey"/>, routes it to the path's current frontier tier, and has each path
    /// independently claim <c>pie × activity ÷ max(playerRating, enemyRating)</c> of XP, then applies the
    /// leveling against each proficiency's authored curve and resolves the milestone/open triggers a crossed
    /// tier reveals.
    /// </para>
    /// <para>
    /// Pure and reference-data-free beyond the injected <see cref="ProficiencyCatalog"/>: current progress is
    /// read via <paramref name="levelOf"/>/<paramref name="xpOf"/> and written via
    /// <paramref name="setProgress"/>, so the live adapter backs them with a <c>PlayerProgress</c> aggregate
    /// while the offline simulator backs them with its own in-loop working state — no aggregate/persistence
    /// coupling here. Milestone reward-skill grants are <em>reported</em> only
    /// (<see cref="ProficiencyXpResult.GrantedSkillIds"/>) — applying them (idempotent <c>Player.UnlockSkill</c>)
    /// and raising the client push are the caller's job, since this method has no <c>Player</c>/<c>ISkills</c>
    /// dependency.
    /// </para>
    /// </summary>
    public static class ProficiencyAccrual
    {
        /// <summary>
        /// Accrues one won battle's proficiency XP. <paramref name="ratingDenominator"/> is
        /// <c>max(playerRating, enemyRating)</c> (spike #1526 Decision 5); <paramref name="levelOf"/>/
        /// <paramref name="xpOf"/> read a proficiency's current (level, xp) — 0/0 for a never-trained
        /// proficiency, matching the row-presence "never trained" convention; <paramref name="setProgress"/>
        /// writes the absolute post-accrual (level, xp) for a proficiency touched this battle.
        /// </summary>
        public static ProficiencyAccrualResult Accrue(
            ProficiencyCatalog catalog, BattleStats stats, double ratingDenominator,
            Func<int, int> levelOf, Func<int, decimal> xpOf, Action<int, int, decimal> setProgress)
        {
            var slices = ProficiencyXpCalculator.Split(
                ServerGameConstants.ProficiencyXpPerVictory, ratingDenominator, BuildActivities(catalog, stats, levelOf));
            if (slices.Count == 0)
            {
                return ProficiencyAccrualResult.Empty;
            }

            var results = new List<ProficiencyXpResult>();
            var opened = new List<ProficiencyOpened>();
            var openedIds = new HashSet<int>();
            foreach (var slice in slices)
            {
                // The slice's proficiency is the path's frontier tier — un-maxed by construction (the routing
                // skips fully-maxed paths and resolves a partially-maxed path to its first un-maxed tier), so a
                // maxed proficiency never reaches here. That routing, not a downstream guard, is what banks
                // nothing on a maxed path now.
                var proficiency = catalog.ResolveProficiency(slice.ProficiencyId);

                var oldLevel = levelOf(slice.ProficiencyId);
                var oldXp = xpOf(slice.ProficiencyId);

                // Round to the persisted XP scale. A trivial enemy's slice — a path's activity tiny relative to
                // the player's power — can round to nothing; skip it rather than persist an information-free
                // zero-gain row.
                var xpGain = Math.Round((decimal)slice.Xp, 3, MidpointRounding.AwayFromZero);
                if (xpGain <= 0)
                {
                    continue;
                }

                var (newLevel, newXp) = proficiency.ApplyXp(oldLevel, oldXp, xpGain);
                setProgress(slice.ProficiencyId, newLevel, newXp);

                // The reward skill of every milestone the gain crossed. Granting it (UnlockSkill is idempotent,
                // so a caller re-applying the same crossing never double-grants) is the caller's job.
                var grantedSkillIds = proficiency.RewardSkillsCrossed(oldLevel, newLevel);

                // Maxing a tier opens the next nodes: its within-path successor, plus any cross-path gateway
                // whose prerequisites are now all maxed. Opening is notification-only — no skill is granted (the
                // opened tier's native skill is re-homed onto this tier's max-level milestone reward, reported
                // above via GrantedSkillIds).
                if (proficiency.IsMaxed(newLevel) && !proficiency.IsMaxed(oldLevel))
                {
                    OpenSuccessors(catalog, proficiency, levelOf, opened, openedIds);
                }

                results.Add(new ProficiencyXpResult(
                    slice.ProficiencyId, xpGain, newLevel, newXp,
                    proficiency.MilestonesCrossed(oldLevel, newLevel), grantedSkillIds));
            }

            return new ProficiencyAccrualResult(results, opened);
        }

        // Opens the nodes a just-maxed proficiency unlocks: the next tier within its own path (revealed by
        // maxing the tier before it — spike #982 decision 10), and any cross-path gateway it gates whose every
        // prerequisite is now maxed (decision 10's themed gateways). Within-path order is implicit in the
        // ordinals, so the successor opens unconditionally here — safe only because a non-root tier can never
        // carry its own authored prerequisites (enforced at admin save time and by the Content Health lint,
        // #2236), so a within-path successor never has a gateway condition of its own to also satisfy.
        private static void OpenSuccessors(
            ProficiencyCatalog catalog, Proficiency maxed, Func<int, int> levelOf,
            List<ProficiencyOpened> opened, HashSet<int> openedIds)
        {
            if (catalog.ResolvePath(maxed.PathId).NextTier(maxed.PathOrdinal) is { } nextTier)
            {
                Open(nextTier.ProficiencyId, opened, openedIds);
            }

            foreach (var gatedId in catalog.DependentsOf(maxed.Id))
            {
                if (AllPrerequisitesMaxed(catalog, gatedId, levelOf))
                {
                    Open(gatedId, opened, openedIds);
                }
            }
        }

        // Records the open for the client push (notification-only — opening grants no skill; the opened tier's
        // native training skill is re-homed onto the predecessor tier's max-level milestone reward). De-duped
        // within the battle so two prerequisites maxing in one fight open a shared gateway once.
        private static void Open(int proficiencyId, List<ProficiencyOpened> opened, HashSet<int> openedIds)
        {
            if (!openedIds.Add(proficiencyId))
            {
                return;
            }

            opened.Add(new ProficiencyOpened(proficiencyId));
        }

        // Whether every prerequisite of the gated proficiency is at its cap on the caller's current progress —
        // the shared "is this gateway unlocked" test. The open trigger uses it to decide a maxed proficiency
        // newly satisfies a gateway, and the activity routing uses it to skip a still-locked gateway frontier.
        // A proficiency with no prerequisites (a root or within-path tier) is trivially unlocked.
        private static bool AllPrerequisitesMaxed(ProficiencyCatalog catalog, int gatedId, Func<int, int> levelOf)
        {
            foreach (var prerequisiteId in catalog.ResolveProficiency(gatedId).PrerequisiteIds)
            {
                if (!catalog.ResolveProficiency(prerequisiteId).IsMaxed(levelOf(prerequisiteId)))
                {
                    return false;
                }
            }

            return true;
        }

        // The per-path activity for the battle, routed to each path's frontier tier. See the application-layer
        // predecessor this was extracted from for the full per-avenue rationale (offense/resist books plus the
        // damage-type-neutral event claims); the routing math itself is unchanged by the #1602 extraction.
        private static List<PathActivity> BuildActivities(ProficiencyCatalog catalog, BattleStats stats, Func<int, int> levelOf)
        {
            var activityByKey = new Dictionary<EActivityKey, double>();
            FoldIntoActivityKeys(activityByKey, stats.TypedDamageDealt, key => ActivityKeys.ForDamageKey(key));
            FoldIntoActivityKeys(activityByKey, BuildResistTrainingByType(stats), ActivityKeys.ForDamageKeyResist);

            void AddEvent(EActivityKey key, double amount)
            {
                if (amount > 0)
                {
                    activityByKey[key] = activityByKey.GetValueOrDefault(key) + amount;
                }
            }

            AddEvent(EActivityKey.Crit, stats.CriticalBonusDealt);
            AddEvent(EActivityKey.Dodge, stats.DamageDodged);
            AddEvent(EActivityKey.Heal, stats.PlayerDamageHealed);
            AddEvent(EActivityKey.Reflect, stats.PlayerReflectedDamageDealt);
            AddEvent(EActivityKey.Hex, stats.HexBonusDealt);
            AddEvent(EActivityKey.Momentum, stats.MomentumBonusDealt);
            AddEvent(EActivityKey.Sunder, stats.SunderBonusDealt);
            AddEvent(EActivityKey.Cull, stats.CullBonusDealt);
            AddEvent(EActivityKey.Parry, stats.PlayerCounterDamageDealt);
            AddEvent(EActivityKey.Cadence, stats.CadenceBonusDealt);

            var activities = new List<PathActivity>();
            foreach (var (activityKey, activity) in activityByKey)
            {
                foreach (var path in catalog.PathsForActivityKey(activityKey))
                {
                    if (path.Frontier(levelOf) is { } frontier && AllPrerequisitesMaxed(catalog, frontier.ProficiencyId, levelOf))
                    {
                        activities.Add(new PathActivity(frontier.ProficiencyId, activity));
                    }
                }
            }

            return activities;
        }

        // Folds a book's per-leaf-type quantities into per-activity-key totals: each type adds its quantity to
        // every key its applies() set resolves to, mapped to that book's activity key by toActivityKey (offense
        // or resist). Accumulates into the shared map so both books route through one pass below; offense and
        // resist keys are disjoint, so the two books never collide on a key.
        private static void FoldIntoActivityKeys(
            Dictionary<EActivityKey, double> activityByKey,
            IReadOnlyDictionary<EDamageType, double> quantityByType,
            Func<EDamageTypeKey, EActivityKey?> toActivityKey)
        {
            foreach (var (type, quantity) in quantityByType)
            {
                foreach (var key in DamageTypes.Applies(type))
                {
                    // A key with no activity key for this book (an amp-only weapon key on the resist side) is
                    // skipped — its quantity routes only through the keys that do resolve.
                    if (toActivityKey(key) is EActivityKey activityKey)
                    {
                        activityByKey[activityKey] = activityByKey.GetValueOrDefault(activityKey) + quantity;
                    }
                }
            }
        }

        // The resist book's per-leaf-type training quantity (#1454): each type's pre-mitigation exposure is
        // split into what the player's own type-resistance blocked (TypedDamageResistanceMitigated) and what
        // still landed (the remainder of TypedDamageExposure), weighted separately so a resist path trains
        // faster the more of its exposure it actually blocks. Keyed off TypedDamageExposure (a superset of
        // TypedDamageResistanceMitigated's keys — mitigated is only ever recorded alongside an exposure entry)
        // so a type with no incoming damage this battle contributes nothing.
        private static Dictionary<EDamageType, double> BuildResistTrainingByType(BattleStats stats)
        {
            var trainingByType = new Dictionary<EDamageType, double>();
            foreach (var (type, exposure) in stats.TypedDamageExposure)
            {
                stats.TypedDamageResistanceMitigated.TryGetValue(type, out var mitigated);
                var unmitigated = exposure - mitigated;
                trainingByType[type] =
                    unmitigated * ServerGameConstants.ResistUnmitigatedTrainingRate
                    + mitigated * ServerGameConstants.ResistMitigatedTrainingRate;
            }

            return trainingByType;
        }
    }
}
