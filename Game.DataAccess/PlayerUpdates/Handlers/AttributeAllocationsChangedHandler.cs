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

            // Group-by-first rather than ToDictionary: the (player, attribute) primary key makes a duplicate
            // impossible, but taking the first per key keeps a stray duplicate row from throwing here.
            var rowsByAttributeId = currentRows
                .GroupBy(pa => pa.AttributeId)
                .ToDictionary(g => g.Key, g => g.First());

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
    }
}
