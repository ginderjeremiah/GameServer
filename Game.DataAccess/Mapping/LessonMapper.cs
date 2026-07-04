using Game.Core;
using Contracts = Game.Abstractions.Contracts;
using EntityLesson = Game.Infrastructure.Entities.Lesson;
using EntityLessonStep = Game.Infrastructure.Entities.LessonStep;

namespace Game.DataAccess.Mapping
{
    internal static class LessonMapper
    {
        /// <summary>Maps an entity <see cref="EntityLesson"/> (with its steps loaded) to the read/authoring
        /// contract. Steps are ordered by their <see cref="EntityLessonStep.Order"/> column, since array position
        /// in the contract's <see cref="Contracts.Lesson.Steps"/> list IS the order.</summary>
        public static Contracts.Lesson ToContract(EntityLesson entity)
        {
            return new Contracts.Lesson
            {
                Id = entity.Id,
                Key = entity.Key,
                Name = entity.Name,
                TriggerType = entity.TriggerType,
                TriggerScreenKey = entity.TriggerScreenKey,
                TriggerMechanicEvent = entity.TriggerMechanicEventId is { } id ? (EMechanicEvent)id : null,
                HostScreenKey = entity.HostScreenKey,
                DisplayOrder = entity.DisplayOrder,
                RetiredAt = entity.RetiredAt,
                Steps = entity.Steps
                    .OrderBy(s => s.Order)
                    .Select(s => new Contracts.LessonStep { Text = s.Text, AnchorKey = s.AnchorKey })
                    .ToList(),
            };
        }

        /// <summary>Maps a read/authoring <see cref="Contracts.Lesson"/> back to its entity graph (steps ordered
        /// by their array position) for the content seeder.</summary>
        public static EntityLesson ToEntity(Contracts.Lesson contract)
        {
            return new EntityLesson
            {
                Id = contract.Id,
                Key = contract.Key,
                Name = contract.Name,
                TriggerType = contract.TriggerType,
                TriggerScreenKey = contract.TriggerScreenKey,
                TriggerMechanicEventId = contract.TriggerMechanicEvent is { } evt ? (int)evt : null,
                HostScreenKey = contract.HostScreenKey,
                DisplayOrder = contract.DisplayOrder,
                RetiredAt = contract.RetiredAt,
                Steps = contract.Steps
                    .Select((s, i) => new EntityLessonStep { LessonId = contract.Id, Order = i, Text = s.Text, AnchorKey = s.AnchorKey })
                    .ToList(),
            };
        }
    }
}
