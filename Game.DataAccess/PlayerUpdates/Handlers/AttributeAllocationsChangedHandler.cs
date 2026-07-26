using Game.Core.Players.Events;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.PlayerUpdates.Handlers
{
    internal sealed class AttributeAllocationsChangedHandler(GameContext context) : IPlayerUpdateHandler<AttributeAllocationsChangedEvent>
    {
        public async Task HandleAsync(AttributeAllocationsChangedEvent evt)
        {
            // The load-then-upsert isn't atomic, so a concurrent apply of the same at-least-once event can
            // insert a (player, attribute) row between our load and save. On the resulting unique violation,
            // clear and re-run once: the now-existing row loads as an update, so the second pass carries no
            // conflicting insert. A second failure propagates to the queue's retry policy rather than looping.
            try
            {
                await ApplyAsync(evt);
            }
            catch (DbUpdateException ex) when (ex.IsUniqueViolation())
            {
                context.ChangeTracker.Clear();
                await ApplyAsync(evt);
            }
        }

        private async Task ApplyAsync(AttributeAllocationsChangedEvent evt)
        {
            var currentRows = await context.PlayerAttributes
                .Where(pa => pa.PlayerId == evt.PlayerId)
                .ToListAsync();

            // The (player, attribute) primary key makes a duplicate impossible; ToFirstByKey still defends
            // against a stray duplicate row throwing here (see InsertIfMissingAsync's sibling helper).
            var rowsByAttributeId = currentRows.ToFirstByKey(pa => pa.AttributeId);

            // Absolute upsert, zeros included: unlike the progress tier's statistics — where row absence is the
            // "no data yet" state — an allocation row's presence is what makes its attribute allocatable at all
            // (the #488 anti-cheat in PlayerStatPoints), so a stored 0 is the seeded state, not an empty one.
            // Deleting it would permanently block the stat once the player falls through to a DB reload (#2459).
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
