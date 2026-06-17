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
    }
}
