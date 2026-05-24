using Game.Core;
using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Enemies;
using EntityEnemy = Game.Abstractions.Entities.Enemy;
using EntityItem = Game.Abstractions.Entities.Item;
using EntitySkill = Game.Abstractions.Entities.Skill;

namespace Game.DataAccess.Mapping
{
    internal static class EnemyMapper
    {
        /// <summary>
        /// Maps an entity <see cref="EntityEnemy"/> to a domain <see cref="Enemy"/> at the given
        /// <paramref name="level"/>. Skill and item definitions are looked up from the preloaded
        /// catalog lists.
        /// </summary>
        public static Enemy ToCore(
            EntityEnemy entity,
            int level,
            IReadOnlyList<EntitySkill> allSkills,
            IReadOnlyList<EntityItem> allItems)
        {
            var skillLookup = allSkills.ToDictionary(s => s.Id);
            var itemLookup = allItems.ToDictionary(i => i.Id);

            return new Enemy
            {
                Id = entity.Id,
                Name = entity.Name,
                Level = level,
                AttributeDistributions = (entity.AttributeDistributions ?? [])
                    .Select(ad => new AttributeDistribution
                    {
                        AttributeId = (EAttribute)ad.AttributeId,
                        BaseAmount = ad.BaseAmount,
                        AmountPerLevel = ad.AmountPerLevel,
                    }).ToList(),
                Skills = (entity.EnemySkills ?? [])
                    .Where(es => skillLookup.ContainsKey(es.SkillId))
                    .Select(es => SkillMapper.ToCore(skillLookup[es.SkillId]))
                    .ToList(),
                Drops = (entity.EnemyDrops ?? [])
                    .Where(ed => itemLookup.ContainsKey(ed.ItemId))
                    .Select(ed => new EnemyDrop
                    {
                        Item = ItemMapper.ToCore(itemLookup[ed.ItemId]),
                        DropRate = ed.DropRate,
                    }).ToList(),
            };
        }
    }
}
