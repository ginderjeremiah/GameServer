using Game.Core;
using Contracts = Game.Abstractions.Contracts;
using EntityLesson = Game.Infrastructure.Entities.Lesson;
using EntityLessonStep = Game.Infrastructure.Entities.LessonStep;

namespace Game.DataAccess.Mapping
{
    internal static class LessonMapper
    {
        /// <summary>Maps an entity <see cref="EntityLesson"/> (with its steps loaded) to the read/authoring
        /// contract.</summary>
        public static Contracts.Lesson ToContract(EntityLesson entity)
        {
            return new Contracts.Lesson
            {
                Id = entity.Id,
                Key = entity.Key,
                Name = entity.Name,
                TriggerType = (ELessonTriggerType)entity.TriggerType,
                ScreenKey = entity.ScreenKey,
                TriggerMechanicEvent = entity.TriggerMechanicEvent is int mechanicEvent ? (EMechanicEvent)mechanicEvent : null,
                Ordinal = entity.Ordinal,
                DesignerNotes = entity.DesignerNotes,
                RetiredAt = entity.RetiredAt,
                Steps = entity.Steps
                    .OrderBy(s => s.Ordinal)
                    .Select(s => new Contracts.LessonStep
                    {
                        Ordinal = s.Ordinal,
                        Text = s.Text,
                        AnchorKey = s.AnchorKey,
                    }).ToList(),
            };
        }

        /// <summary>Maps a read/authoring <see cref="Contracts.Lesson"/> back to its entity graph (steps
        /// included) for the content seeder.</summary>
        public static EntityLesson ToEntity(Contracts.Lesson contract)
        {
            return new EntityLesson
            {
                Id = contract.Id,
                Key = contract.Key,
                Name = contract.Name,
                TriggerType = (int)contract.TriggerType,
                ScreenKey = contract.ScreenKey,
                TriggerMechanicEvent = contract.TriggerMechanicEvent is EMechanicEvent mechanicEvent ? (int)mechanicEvent : null,
                Ordinal = contract.Ordinal,
                DesignerNotes = contract.DesignerNotes,
                RetiredAt = contract.RetiredAt,
                Steps = contract.Steps
                    .Select(s => new EntityLessonStep
                    {
                        LessonId = contract.Id,
                        Ordinal = s.Ordinal,
                        Text = s.Text,
                        AnchorKey = s.AnchorKey,
                    }).ToList(),
            };
        }
    }
}
