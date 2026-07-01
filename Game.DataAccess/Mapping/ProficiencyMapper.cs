using Game.Core;
using Game.Core.Proficiencies;
using Contracts = Game.Abstractions.Contracts;
using EntityProficiency = Game.Infrastructure.Entities.Proficiency;
using EntityProficiencyLevelModifier = Game.Infrastructure.Entities.ProficiencyLevelModifier;
using EntityProficiencyLevelReward = Game.Infrastructure.Entities.ProficiencyLevelReward;
using EntityProficiencyPrerequisite = Game.Infrastructure.Entities.ProficiencyPrerequisite;
using CoreProficiency = Game.Core.Proficiencies.Proficiency;

namespace Game.DataAccess.Mapping
{
    internal static class ProficiencyMapper
    {
        /// <summary>Maps an entity <see cref="EntityProficiency"/> (with its level modifiers, level rewards,
        /// prerequisites, and skill contributions loaded) to the read/authoring contract.</summary>
        public static Contracts.Proficiency ToContract(EntityProficiency entity)
        {
            return new Contracts.Proficiency
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                IconPath = entity.IconPath,
                Word = entity.Word,
                Pronunciation = entity.Pronunciation,
                Translation = entity.Translation,
                PathId = entity.PathId,
                PathOrdinal = entity.PathOrdinal,
                MaxLevel = entity.MaxLevel,
                BaseXp = entity.BaseXp,
                XpGrowth = entity.XpGrowth,
                DesignerNotes = entity.DesignerNotes,
                RetiredAt = entity.RetiredAt,
                LevelModifiers = entity.LevelModifiers
                    .Select(m => new Contracts.ProficiencyLevelModifier
                    {
                        Level = m.Level,
                        AttributeId = (EAttribute)m.AttributeId,
                        ModifierTypeId = (EModifierType)m.ModifierType,
                        Amount = m.Amount,
                    }).ToList(),
                LevelRewards = entity.LevelRewards
                    .Select(r => new Contracts.ProficiencyLevelReward
                    {
                        Level = r.Level,
                        RewardSkillId = r.RewardSkillId,
                    }).ToList(),
                PrerequisiteIds = entity.Prerequisites
                    .Select(p => p.PrerequisiteProficiencyId).ToList(),
            };
        }

        /// <summary>Maps a read/authoring <see cref="Contracts.Proficiency"/> back to its entity graph (level
        /// modifiers, level rewards, and cross-path prerequisite edges) for the content seeder.</summary>
        public static EntityProficiency ToEntity(Contracts.Proficiency contract)
        {
            return new EntityProficiency
            {
                Id = contract.Id,
                Name = contract.Name,
                Description = contract.Description,
                IconPath = contract.IconPath,
                Word = contract.Word,
                Pronunciation = contract.Pronunciation,
                Translation = contract.Translation,
                PathId = contract.PathId,
                PathOrdinal = contract.PathOrdinal,
                MaxLevel = contract.MaxLevel,
                BaseXp = contract.BaseXp,
                XpGrowth = contract.XpGrowth,
                DesignerNotes = contract.DesignerNotes,
                RetiredAt = contract.RetiredAt,
                LevelModifiers = contract.LevelModifiers
                    .Select(m => new EntityProficiencyLevelModifier
                    {
                        ProficiencyId = contract.Id,
                        Level = m.Level,
                        AttributeId = (int)m.AttributeId,
                        ModifierType = (int)m.ModifierTypeId,
                        Amount = m.Amount,
                    }).ToList(),
                LevelRewards = contract.LevelRewards
                    .Select(r => new EntityProficiencyLevelReward
                    {
                        ProficiencyId = contract.Id,
                        Level = r.Level,
                        RewardSkillId = r.RewardSkillId,
                    }).ToList(),
                Prerequisites = contract.PrerequisiteIds
                    .Select(prerequisiteId => new EntityProficiencyPrerequisite
                    {
                        ProficiencyId = contract.Id,
                        PrerequisiteProficiencyId = prerequisiteId,
                    }).ToList(),
            };
        }

        /// <summary>Maps an entity <see cref="EntityProficiency"/> (with its child collections loaded) to a
        /// domain <see cref="CoreProficiency"/>, grouping the flat per-level modifier/reward rows into one
        /// ascending <see cref="ProficiencyLevel"/> list.</summary>
        public static CoreProficiency ToCore(EntityProficiency entity)
        {
            var modifiersByLevel = entity.LevelModifiers
                .GroupBy(m => m.Level)
                .ToDictionary(
                    g => g.Key,
                    g => (IReadOnlyList<ProficiencyModifier>)g
                        .Select(m => new ProficiencyModifier
                        {
                            Attribute = (EAttribute)m.AttributeId,
                            ModifierType = (EModifierType)m.ModifierType,
                            Amount = (double)m.Amount,
                        }).ToList());

            var rewardByLevel = entity.LevelRewards.ToDictionary(r => r.Level, r => r.RewardSkillId);

            var levels = modifiersByLevel.Keys
                .Union(rewardByLevel.Keys)
                .OrderBy(level => level)
                .Select(level => new ProficiencyLevel
                {
                    Level = level,
                    Modifiers = modifiersByLevel.TryGetValue(level, out var modifiers) ? modifiers : [],
                    RewardSkillId = rewardByLevel.TryGetValue(level, out var skillId) ? skillId : null,
                }).ToList();

            return new CoreProficiency
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                PathId = entity.PathId,
                PathOrdinal = entity.PathOrdinal,
                MaxLevel = entity.MaxLevel,
                BaseXp = (double)entity.BaseXp,
                XpGrowth = (double)entity.XpGrowth,
                PrerequisiteIds = entity.Prerequisites.Select(p => p.PrerequisiteProficiencyId).ToList(),
                Levels = levels,
            };
        }
    }
}
