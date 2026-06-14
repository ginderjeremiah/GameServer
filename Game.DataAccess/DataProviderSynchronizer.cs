using Game.Infrastructure.Entities;
using Game.Abstractions.Infrastructure;
using Game.Core;
using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Game.DataAccess
{
    internal class DataProviderSynchronizer : IHostedService
    {
        private readonly IServiceProvider _services;
        private readonly IPubSubService _pubsub;
        private readonly ILogger<DataProviderSynchronizer> _logger;
        private readonly PlayerUpdateRetryPolicy _retryPolicy;

        // Serializes queue drains so at most one runs at a time. The pub/sub background worker re-arms its wait
        // independently of callback completion, so a second wake arriving mid-drain can dispatch a second
        // ProcessQueue on another thread-pool thread; both then pop from the same queue via atomic LPOP and can
        // apply two order-sensitive same-player events (equip→unequip, the delete-then-rebuild loadout handler)
        // out of order, persisting stale state until the cache self-heals on the next save (#578). WaitAsync(0)
        // never materializes a wait handle, so the semaphore is intentionally left undisposed (this is a
        // process-lifetime hosted service anyway) — the same rationale as the per-socket command/send locks.
        private readonly SemaphoreSlim _drainGate = new(1, 1);

        // 0/1 wake flag, accessed only through Interlocked/Volatile. A caller sets it before trying to claim the
        // gate, and the active drainer re-checks it after releasing, so a wake that races the drainer's exit is
        // never stranded — it is coalesced into one follow-up pass rather than dropped.
        private int _drainRequested;

        public DataProviderSynchronizer(IServiceProvider services, IPubSubService pubsub, ILogger<DataProviderSynchronizer> logger, PlayerUpdateRetryPolicy retryPolicy)
        {
            _services = services;
            _pubsub = pubsub;
            _logger = logger;
            _retryPolicy = retryPolicy;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await InitSubscriber();
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private async Task InitSubscriber()
        {
            await _pubsub.Subscribe(
                Constants.PUBSUB_PLAYER_CHANNEL,
                Constants.PUBSUB_PLAYER_QUEUE,
                async args => await ProcessQueue(args.queue));
        }

        internal async Task ProcessQueue(IPubSubQueue queue)
        {
            // Record the wake before attempting to claim the gate. If another drain is already in progress it
            // will observe this flag when it re-checks after releasing (below) and drain on our behalf, so the
            // events we were woken for are never missed even though we return without draining ourselves.
            Interlocked.Exchange(ref _drainRequested, 1);

            // Re-check after each release: a wake that set the flag while we held the gate failed to claim it
            // (WaitAsync(0) returned false for that caller), so we loop back to honor it rather than strand it.
            while (Volatile.Read(ref _drainRequested) == 1 && await _drainGate.WaitAsync(0))
            {
                try
                {
                    // Claim the pending wake(s) one pass at a time. Exchanging the flag to 0 before draining means
                    // a wake arriving during the drain re-sets it to 1, so the loop runs another pass and picks up
                    // the freshly enqueued events — the whole queue is drained without ever running two passes
                    // concurrently.
                    while (Interlocked.Exchange(ref _drainRequested, 0) == 1)
                    {
                        await DrainQueueAsync(queue);
                    }
                }
                finally
                {
                    _drainGate.Release();
                }
            }
        }

        private async Task DrainQueueAsync(IPubSubQueue queue)
        {
            var deadLetterQueue = _pubsub.GetQueue(Constants.PUBSUB_PLAYER_DEAD_LETTER_QUEUE);

            var next = await queue.GetNextAsync();
            while (next is not null)
            {
                await ProcessMessage(next, deadLetterQueue);
                next = await queue.GetNextAsync();
            }
        }

        /// <summary>
        /// Processes a single queued message. Malformed payloads (which can never succeed) are dead-lettered
        /// immediately, while a valid event that fails on an unexpected error (e.g. a transient database error)
        /// is retried with exponential backoff per <see cref="PlayerUpdateRetryPolicy"/> and dead-lettered only
        /// once the retries are exhausted, so the change is never silently dropped.
        /// </summary>
        private async Task ProcessMessage(string message, IPubSubQueue deadLetterQueue)
        {
            DomainEventEnvelope? envelope;
            try
            {
                envelope = message.Deserialize<DomainEventEnvelope>();
            }
            catch (JsonException ex)
            {
                // A malformed payload can never be parsed successfully, so it is dead-lettered for inspection rather than retried.
                _logger.LogWarning(ex, "Dead-lettering malformed player data event from queue '{Queue}'. Raw message: {Message}", Constants.PUBSUB_PLAYER_QUEUE, message);
                await deadLetterQueue.AddToQueueAsync(message);
                return;
            }

            if (envelope is null)
            {
                // A null payload deserialized cleanly but carries no event to apply; dead-letter it rather than silently dropping it.
                _logger.LogWarning("Dead-lettering empty player data event from queue '{Queue}'. Raw message: {Message}", Constants.PUBSUB_PLAYER_QUEUE, message);
                await deadLetterQueue.AddToQueueAsync(message);
                return;
            }

            for (var attempt = 1; attempt <= _retryPolicy.MaxAttempts; attempt++)
            {
                try
                {
                    await HandleEvent(envelope);
                    return;
                }
                catch (JsonException ex)
                {
                    // A malformed inner payload is a poison message that no retry can fix, so it is dead-lettered immediately.
                    _logger.LogWarning(ex, "Dead-lettering player data event '{EventType}' with a malformed payload from queue '{Queue}'. Raw message: {Message}", envelope.Type, Constants.PUBSUB_PLAYER_QUEUE, message);
                    await deadLetterQueue.AddToQueueAsync(message);
                    return;
                }
                catch (UnknownEventTypeException ex)
                {
                    // An unrecognized event type is a poison message — no retry can fix it — so it is dead-lettered immediately.
                    _logger.LogWarning(ex, "Dead-lettering player data event with unrecognized type '{EventType}' from queue '{Queue}'. Raw message: {Message}", envelope.Type, Constants.PUBSUB_PLAYER_QUEUE, message);
                    await deadLetterQueue.AddToQueueAsync(message);
                    return;
                }
                catch (Exception ex) when (attempt < _retryPolicy.MaxAttempts)
                {
                    // An unexpected failure (e.g. a transient database error) may succeed on a retry.
                    _logger.LogWarning(ex, "Failed to process player data event '{EventType}' from queue '{Queue}' on attempt {Attempt} of {MaxAttempts}; retrying.", envelope.Type, Constants.PUBSUB_PLAYER_QUEUE, attempt, _retryPolicy.MaxAttempts);
                    await Task.Delay(_retryPolicy.DelayAfterAttempt(attempt));
                }
                catch (Exception ex)
                {
                    // Retries exhausted: the change could not be persisted, so the event is dead-lettered for later inspection/replay instead of being dropped.
                    _logger.LogError(ex, "Failed to process player data event '{EventType}' from queue '{Queue}' after {MaxAttempts} attempts; dead-lettering. Raw message: {Message}", envelope.Type, Constants.PUBSUB_PLAYER_QUEUE, _retryPolicy.MaxAttempts, message);
                    await deadLetterQueue.AddToQueueAsync(message);
                    return;
                }
            }
        }

        private async Task HandleEvent(DomainEventEnvelope envelope)
        {
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();

            switch (envelope.Type)
            {
                case nameof(PlayerCoreUpdatedEvent):
                    var coreEvt = Deserialize<PlayerCoreUpdatedEvent>(envelope.Payload);
                    await HandlePlayerCoreUpdated(context, coreEvt);
                    break;

                case nameof(AttributeAllocationsChangedEvent):
                    var attrEvt = Deserialize<AttributeAllocationsChangedEvent>(envelope.Payload);
                    await HandleAttributeAllocationsChanged(context, attrEvt);
                    break;

                case nameof(ItemUnlockedEvent):
                    var unlockEvt = Deserialize<ItemUnlockedEvent>(envelope.Payload);
                    await HandleItemUnlocked(context, unlockEvt);
                    break;

                case nameof(ItemEquippedEvent):
                    var equipEvt = Deserialize<ItemEquippedEvent>(envelope.Payload);
                    await HandleItemEquipped(context, equipEvt);
                    break;

                case nameof(ItemUnequippedEvent):
                    var unequipEvt = Deserialize<ItemUnequippedEvent>(envelope.Payload);
                    await HandleItemUnequipped(context, unequipEvt);
                    break;

                case nameof(ModUnlockedEvent):
                    var modUnlockEvt = Deserialize<ModUnlockedEvent>(envelope.Payload);
                    await HandleModUnlocked(context, modUnlockEvt);
                    break;

                case nameof(ModAppliedEvent):
                    var modApplyEvt = Deserialize<ModAppliedEvent>(envelope.Payload);
                    await HandleModApplied(context, modApplyEvt);
                    break;

                case nameof(ModRemovedEvent):
                    var modRemoveEvt = Deserialize<ModRemovedEvent>(envelope.Payload);
                    await HandleModRemoved(context, modRemoveEvt);
                    break;

                case nameof(SkillUnlockedEvent):
                    var skillUnlockEvt = Deserialize<SkillUnlockedEvent>(envelope.Payload);
                    await HandleSkillUnlocked(context, skillUnlockEvt);
                    break;

                case nameof(SelectedSkillsChangedEvent):
                    var selectedSkillsEvt = Deserialize<SelectedSkillsChangedEvent>(envelope.Payload);
                    await HandleSelectedSkillsChanged(context, selectedSkillsEvt);
                    break;

                case nameof(ItemFavoriteChangedEvent):
                    var favoriteEvt = Deserialize<ItemFavoriteChangedEvent>(envelope.Payload);
                    await HandleItemFavoriteChanged(context, favoriteEvt);
                    break;

                case nameof(LogPreferenceChangedEvent):
                    var logEvt = Deserialize<LogPreferenceChangedEvent>(envelope.Payload);
                    await HandleLogPreferenceChanged(context, logEvt);
                    break;

                case nameof(ProgressUpdatedEvent):
                    var progressEvt = Deserialize<ProgressUpdatedEvent>(envelope.Payload);
                    await HandleProgressUpdated(context, progressEvt);
                    break;

                // PlayerLeveledUpEvent is handled in-process only — it has no persistence
                // handler registered, so it is never published to this queue.

                default:
                    throw new UnknownEventTypeException(envelope.Type);
            }
        }

        private static async Task HandlePlayerCoreUpdated(GameContext context, PlayerCoreUpdatedEvent evt)
        {
            await context.Players
                .Where(p => p.Id == evt.PlayerId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Level, evt.Level)
                    .SetProperty(p => p.Exp, evt.Exp)
                    .SetProperty(p => p.CurrentZoneId, evt.CurrentZoneId)
                    .SetProperty(p => p.StatPointsGained, evt.StatPointsGained)
                    .SetProperty(p => p.StatPointsUsed, evt.StatPointsUsed));
        }

        private static async Task HandleAttributeAllocationsChanged(GameContext context, AttributeAllocationsChangedEvent evt)
        {
            var currentRows = await context.PlayerAttributes
                .Where(pa => pa.PlayerId == evt.PlayerId)
                .ToListAsync();

            var rowsByAttributeId = currentRows.ToDictionary(pa => pa.AttributeId);

            foreach (var alloc in evt.Allocations)
            {
                var attributeId = (int)alloc.Attribute;
                var amount = (decimal)alloc.Amount;

                if (rowsByAttributeId.TryGetValue(attributeId, out var row))
                {
                    if (amount == 0)
                    {
                        context.PlayerAttributes.Remove(row);
                    }
                    else
                    {
                        row.Amount = amount;
                    }
                }
                else if (amount != 0)
                {
                    context.PlayerAttributes.Add(new PlayerAttribute
                    {
                        PlayerId = evt.PlayerId,
                        AttributeId = attributeId,
                        Amount = amount,
                    });
                }
            }

            await context.SaveChangesAsync();
        }

        private static async Task HandleItemUnlocked(GameContext context, ItemUnlockedEvent evt)
        {
            var exists = await context.UnlockedItems
                .AnyAsync(ui => ui.PlayerId == evt.PlayerId && ui.ItemId == evt.ItemId);

            if (!exists)
            {
                context.UnlockedItems.Add(new UnlockedItem
                {
                    PlayerId = evt.PlayerId,
                    ItemId = evt.ItemId,
                });
                await context.SaveChangesAsync();
            }
        }

        private static async Task HandleItemEquipped(GameContext context, ItemEquippedEvent evt)
        {
            // Absolute per-slot upsert in a single atomic UPDATE: the equipped item takes the slot and any
            // prior occupant is cleared, in one statement. The previous two-statement clear-then-set left a
            // window where a crash between the writes (the queue read is a destructive pop, so the event is
            // not redelivered) emptied the slot for good. Folding both into one CASE-driven update over the
            // affected rows removes that window and stays idempotent — re-applying converges to the same state,
            // including when the item is moving from another slot (its own row is reassigned, vacating the old).
            await context.UnlockedItems
                .Where(ui => ui.PlayerId == evt.PlayerId && (ui.ItemId == evt.ItemId || ui.EquipmentSlotId == evt.SlotId))
                .ExecuteUpdateAsync(s => s.SetProperty(
                    ui => ui.EquipmentSlotId,
                    ui => ui.ItemId == evt.ItemId ? evt.SlotId : (int?)null));
        }

        private static async Task HandleItemUnequipped(GameContext context, ItemUnequippedEvent evt)
        {
            await context.UnlockedItems
                .Where(ui => ui.PlayerId == evt.PlayerId && ui.ItemId == evt.ItemId)
                .ExecuteUpdateAsync(s => s.SetProperty(ui => ui.EquipmentSlotId, (int?)null));
        }

        private static async Task HandleModUnlocked(GameContext context, ModUnlockedEvent evt)
        {
            var exists = await context.UnlockedMods
                .AnyAsync(um => um.PlayerId == evt.PlayerId && um.ItemModId == evt.ItemModId);

            if (!exists)
            {
                context.UnlockedMods.Add(new UnlockedMod
                {
                    PlayerId = evt.PlayerId,
                    ItemModId = evt.ItemModId,
                });
                await context.SaveChangesAsync();
            }
        }

        private static async Task HandleSkillUnlocked(GameContext context, SkillUnlockedEvent evt)
        {
            var exists = await context.PlayerSkills
                .AnyAsync(ps => ps.PlayerId == evt.PlayerId && ps.SkillId == evt.SkillId);

            if (!exists)
            {
                // Earning a skill unlocks it without equipping it: Selected = false, Order = 0
                // (the player chooses their loadout separately). Idempotent insert mirrors the
                // item/mod unlock handlers so re-applying the event never duplicates the row.
                context.PlayerSkills.Add(new PlayerSkill
                {
                    PlayerId = evt.PlayerId,
                    SkillId = evt.SkillId,
                    Selected = false,
                    Order = 0,
                });
                await context.SaveChangesAsync();
            }
        }

        private static async Task HandleSelectedSkillsChanged(GameContext context, SelectedSkillsChangedEvent evt)
        {
            // Delete-then-rebuild for idempotency, applied as a single write (the same shape as the
            // attribute-allocations handler): fetch the player's skill rows, reset every flag, then mark
            // each id in the ordered loadout Selected = true with its index as Order. Re-applying the event
            // converges to the same state, and EF batches the touched rows into one round-trip rather than
            // issuing one ExecuteUpdate per skill.
            var playerSkills = await context.PlayerSkills
                .Where(ps => ps.PlayerId == evt.PlayerId)
                .ToListAsync();

            var orderBySkillId = new Dictionary<int, int>(evt.OrderedSkillIds.Count);
            for (var index = 0; index < evt.OrderedSkillIds.Count; index++)
            {
                orderBySkillId[evt.OrderedSkillIds[index]] = index;
            }

            foreach (var playerSkill in playerSkills)
            {
                if (orderBySkillId.TryGetValue(playerSkill.SkillId, out var order))
                {
                    playerSkill.Selected = true;
                    playerSkill.Order = order;
                }
                else
                {
                    playerSkill.Selected = false;
                    playerSkill.Order = 0;
                }
            }

            await context.SaveChangesAsync();
        }

        private static async Task HandleModApplied(GameContext context, ModAppliedEvent evt)
        {
            // Remove existing mod in the same slot
            await context.AppliedMods
                .Where(am => am.PlayerId == evt.PlayerId && am.ItemId == evt.ItemId && am.ItemModSlotId == evt.ItemModSlotId)
                .ExecuteDeleteAsync();

            context.AppliedMods.Add(new AppliedMod
            {
                PlayerId = evt.PlayerId,
                ItemId = evt.ItemId,
                ItemModSlotId = evt.ItemModSlotId,
                ItemModId = evt.ItemModId,
            });
            await context.SaveChangesAsync();
        }

        private static async Task HandleModRemoved(GameContext context, ModRemovedEvent evt)
        {
            await context.AppliedMods
                .Where(am => am.PlayerId == evt.PlayerId && am.ItemId == evt.ItemId && am.ItemModSlotId == evt.ItemModSlotId)
                .ExecuteDeleteAsync();
        }

        private static async Task HandleItemFavoriteChanged(GameContext context, ItemFavoriteChangedEvent evt)
        {
            // Idempotent absolute update: set the flag to the event's value for the player's unlocked item.
            await context.UnlockedItems
                .Where(ui => ui.PlayerId == evt.PlayerId && ui.ItemId == evt.ItemId)
                .ExecuteUpdateAsync(s => s.SetProperty(ui => ui.Favorite, evt.Favorite));
        }

        private static async Task HandleLogPreferenceChanged(GameContext context, LogPreferenceChangedEvent evt)
        {
            var logTypeId = (int)evt.LogType;

            // Idempotent upsert mirroring HandleItemFavoriteChanged: attempt the absolute update first as a
            // single self-committing write; if no row exists yet (rows-affected 0) fall through to the insert.
            // Re-applying the event converges to the same state under the write-behind retry policy.
            var updated = await context.LogPreferences
                .Where(lp => lp.PlayerId == evt.PlayerId && lp.LogTypeId == logTypeId)
                .ExecuteUpdateAsync(s => s.SetProperty(lp => lp.Enabled, evt.Enabled));

            if (updated == 0)
            {
                context.LogPreferences.Add(new LogPreference
                {
                    PlayerId = evt.PlayerId,
                    LogTypeId = logTypeId,
                    Enabled = evt.Enabled,
                });
                await context.SaveChangesAsync();
            }
        }

        private static async Task HandleProgressUpdated(GameContext context, ProgressUpdatedEvent evt)
        {
            // Absolute upserts so re-applying the event under the retry policy converges to the same state.
            // Batched like HandleAttributeAllocationsChanged: load the touched rows, set/insert, save once.
            if (evt.Statistics.Count > 0)
            {
                // Constrain the load to the touched (type, entity) space, not every row of the touched
                // types: a long-lived account accrues one row per enemy/skill, so filtering on typeIds alone
                // would load hundreds to upsert the ~10-20 this battle changed (aggregate-DB-load concern,
                // #548). The exact-key match still happens in memory below; this only bounds the fetch.
                // entityIds includes null for the global rows — EF turns Contains over a List<int?> into
                // "EntityId IN (...) OR EntityId IS NULL".
                var typeIds = evt.Statistics.Select(s => s.StatisticTypeId).Distinct().ToList();
                var entityIds = evt.Statistics.Select(s => s.EntityId).Distinct().ToList();
                var existing = await context.PlayerStatistics
                    .Where(ps => ps.PlayerId == evt.PlayerId
                        && typeIds.Contains(ps.StatisticTypeId)
                        && entityIds.Contains(ps.EntityId))
                    .ToListAsync();
                var byKey = existing.ToDictionary(ps => (ps.StatisticTypeId, ps.EntityId));

                foreach (var stat in evt.Statistics)
                {
                    if (byKey.TryGetValue((stat.StatisticTypeId, stat.EntityId), out var row))
                    {
                        row.Value = stat.Value;
                    }
                    else
                    {
                        context.PlayerStatistics.Add(new PlayerStatistic
                        {
                            PlayerId = evt.PlayerId,
                            StatisticTypeId = stat.StatisticTypeId,
                            EntityId = stat.EntityId,
                            Value = stat.Value,
                        });
                    }
                }
            }

            if (evt.Challenges.Count > 0)
            {
                var challengeIds = evt.Challenges.Select(c => c.ChallengeId).ToList();
                var existing = await context.PlayerChallenges
                    .Where(pc => pc.PlayerId == evt.PlayerId && challengeIds.Contains(pc.ChallengeId))
                    .ToListAsync();
                var byId = existing.ToDictionary(pc => pc.ChallengeId);

                foreach (var challenge in evt.Challenges)
                {
                    if (byId.TryGetValue(challenge.ChallengeId, out var row))
                    {
                        row.Progress = challenge.Progress;
                        row.Completed = challenge.Completed;
                        row.CompletedAt = challenge.CompletedAt;
                    }
                    else
                    {
                        context.PlayerChallenges.Add(new PlayerChallenge
                        {
                            PlayerId = evt.PlayerId,
                            ChallengeId = challenge.ChallengeId,
                            Progress = challenge.Progress,
                            Completed = challenge.Completed,
                            CompletedAt = challenge.CompletedAt,
                        });
                    }
                }
            }

            await context.SaveChangesAsync();
        }

        private static T Deserialize<T>(string json)
            => json.Deserialize<T>() ?? throw new JsonException($"Deserialized '{typeof(T).Name}' payload was null.");

        private sealed class UnknownEventTypeException(string eventType)
            : Exception($"Unrecognized player event type '{eventType}'.");
    }
}
