using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class AttributeAllocationsChangedHandler(PlayerWriteWatermarkGuard guard) : IPlayerUpdateHandler<AttributeAllocationsChangedEvent>
    {
        // A stale replay durably reverts spent stat points while Player.StatPointsUsed may already hold the
        // newer value, leaving the two disagreeing until the player reallocates. Player-scoped — see
        // PlayerWriteStream.AttributeAllocations. The guard owns the transaction, the context the write must
        // be issued on, and the unique-violation restart the load-then-upsert below needs.
        public Task HandleAsync(AttributeAllocationsChangedEvent evt)
            => guard.ExecuteGuardedAsync(
                evt.PlayerId,
                PlayerWriteStream.AttributeAllocations,
                [PlayerWriteWatermarkGuard.PlayerScopedTarget],
                (context, _) => ApplyAsync(context, evt));

        private static async Task ApplyAsync(GameContext context, AttributeAllocationsChangedEvent evt)
        {
            var currentRows = await context.PlayerAttributes
                .Where(pa => pa.PlayerId == evt.PlayerId)
                .ToListAsync();

            // The (player, attribute) primary key makes a duplicate impossible; ToFirstByKey still defends
            // against a stray duplicate row throwing here (see InsertIfMissingAsync's sibling helper).
            var rowsByAttributeId = currentRows.ToFirstByKey(pa => pa.AttributeId);

            // Absolute upsert, zeros included: unlike the progress tier's statistics — where row absence is the
            // "no data yet" state — a zero allocation is a real value the player can reallocate down to, and
            // the complete spread is what the client renders, so a stored 0 is the seeded state, not an empty
            // one. Deleting it would silently drop that reallocation until the next rehydration reseed.
            foreach (var alloc in evt.Allocations)
            {
                var attributeId = (int)alloc.Attribute;
                var amount = (decimal)alloc.Amount;

                if (rowsByAttributeId.TryGetValue(attributeId, out var row))
                {
                    row.Amount = amount;
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
            }

            await context.SaveChangesAsync();
        }
    }
}
