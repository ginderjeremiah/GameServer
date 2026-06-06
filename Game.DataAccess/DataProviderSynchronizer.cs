using Game.Abstractions.Entities;
using Game.Abstractions.Infrastructure;
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

        public DataProviderSynchronizer(IServiceProvider services, IPubSubService pubsub, ILogger<DataProviderSynchronizer> logger)
        {
            _services = services;
            _pubsub = pubsub;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = InitSubscriber();
            return Task.CompletedTask;
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
            var next = await queue.GetNextAsync();
            while (next is not null)
            {
                try
                {
                    var envelope = JsonSerializer.Deserialize<DomainEventEnvelope>(next);
                    if (envelope is not null)
                    {
                        await HandleEvent(envelope);
                    }
                }
                catch (JsonException ex)
                {
                    // A malformed payload can never be parsed successfully, so it is logged and skipped.
                    _logger.LogWarning(ex, "Skipping malformed player data event from queue '{Queue}'. Raw message: {Message}", Constants.PUBSUB_PLAYER_QUEUE, next);
                }
                catch (Exception ex)
                {
                    // An unexpected failure (e.g. a database error) means the player change may not have been persisted.
                    _logger.LogError(ex, "Failed to process player data event from queue '{Queue}'. The player change may not have been persisted. Raw message: {Message}", Constants.PUBSUB_PLAYER_QUEUE, next);
                }

                next = await queue.GetNextAsync();
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

                case nameof(LogPreferenceChangedEvent):
                    var logEvt = Deserialize<LogPreferenceChangedEvent>(envelope.Payload);
                    await HandleLogPreferenceChanged(context, logEvt);
                    break;

                    // PlayerLeveledUpEvent is handled in-process only — it has no persistence
                    // handler registered, so it is never published to this queue.
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
            foreach (var alloc in evt.Allocations)
            {
                var attributeId = (int)alloc.Attribute;
                var amount = (decimal)alloc.Amount;

                if (amount == 0)
                {
                    await context.PlayerAttributes
                        .Where(pa => pa.PlayerId == evt.PlayerId && pa.AttributeId == attributeId)
                        .ExecuteDeleteAsync();
                }
                else
                {
                    var existing = await context.PlayerAttributes
                        .FirstOrDefaultAsync(pa => pa.PlayerId == evt.PlayerId && pa.AttributeId == attributeId);

                    if (existing is not null)
                    {
                        existing.Amount = amount;
                    }
                    else
                    {
                        context.PlayerAttributes.Add(new PlayerAttribute
                        {
                            PlayerId = evt.PlayerId,
                            AttributeId = attributeId,
                            Amount = amount,
                        });
                    }

                    await context.SaveChangesAsync();
                }
            }
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
            // Clear the target slot
            await context.UnlockedItems
                .Where(ui => ui.PlayerId == evt.PlayerId && ui.EquipmentSlotId == evt.SlotId)
                .ExecuteUpdateAsync(s => s.SetProperty(ui => ui.EquipmentSlotId, (int?)null));

            // Equip the item
            await context.UnlockedItems
                .Where(ui => ui.PlayerId == evt.PlayerId && ui.ItemId == evt.ItemId)
                .ExecuteUpdateAsync(s => s.SetProperty(ui => ui.EquipmentSlotId, evt.SlotId));
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

        private static async Task HandleLogPreferenceChanged(GameContext context, LogPreferenceChangedEvent evt)
        {
            var logTypeId = (int)evt.LogType;
            var existing = await context.LogPreferences
                .FirstOrDefaultAsync(lp => lp.PlayerId == evt.PlayerId && lp.LogTypeId == logTypeId);

            if (existing is not null)
            {
                existing.Enabled = evt.Enabled;
            }
            else
            {
                context.LogPreferences.Add(new LogPreference
                {
                    PlayerId = evt.PlayerId,
                    LogTypeId = logTypeId,
                    Enabled = evt.Enabled,
                });
            }

            await context.SaveChangesAsync();
        }

        private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json)!;
    }
}
