using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Content Authoring persistence for tutorial lessons and their ordered tour steps. Reuses the cached
    /// entity lookups for existence/diff and builds fresh, navigation-free entities for every write. The
    /// identity save is retire-only (no hard delete); the step relationship setter reconciles a full desired set.
    /// </summary>
    internal class AdminLessons(ILessonEntityCache lessons, IEntityStore entityStore) : IAdminLessons
    {
        private readonly ILessonEntityCache _lessons = lessons;
        private readonly IEntityStore _entityStore = entityStore;

        public AdminSaveResult SaveLessons(IReadOnlyList<Change<Contracts.Lesson>> changes)
        {
            if (FindTriggerViolation(changes) is { } triggerRejection)
            {
                return triggerRejection;
            }

            if (FindKeyCollision(changes) is { } keyRejection)
            {
                return keyRejection;
            }

            return ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(ToEntity(item)),
                edit: item =>
                {
                    var entity = ToEntity(item);
                    entity.Id = item.Id;
                    entity.RetiredAt = item.RetiredAt;
                    _entityStore.Update(entity);
                },
                key: item => item.Id,
                resourceName: "lesson",
                editExists: item => _lessons.LookupLesson(item.Id) is not null);
        }

        private static Entities.Lesson ToEntity(Contracts.Lesson item)
        {
            return new Entities.Lesson
            {
                Key = item.Key,
                Name = item.Name,
                TriggerType = (int)item.TriggerType,
                ScreenKey = item.ScreenKey,
                TriggerMechanicEvent = item.TriggerMechanicEvent is EMechanicEvent mechanicEvent ? (int)mechanicEvent : null,
                Ordinal = item.Ordinal,
                DesignerNotes = item.DesignerNotes,
            };
        }

        public AdminSaveResult SetSteps(SetLessonStepsData data)
        {
            var lesson = _lessons.LookupLesson(data.Id);
            if (lesson is null)
            {
                return AdminSaveResult.NotFound("Lesson");
            }

            return ChildCollectionReconciler.Reconcile(
                existing: lesson.Steps,
                desired: data.Steps,
                existingKey: s => s.Ordinal,
                desiredKey: s => s.Ordinal,
                delete: s => _entityStore.Delete(new Entities.LessonStep
                {
                    LessonId = lesson.Id,
                    Ordinal = s.Ordinal,
                    Text = "",
                }),
                insert: s => _entityStore.Insert(ToStepEntity(lesson.Id, s)),
                resourceName: "lesson step",
                update: s => _entityStore.Update(ToStepEntity(lesson.Id, s)));
        }

        private static Entities.LessonStep ToStepEntity(int lessonId, Contracts.LessonStep step)
        {
            return new Entities.LessonStep
            {
                LessonId = lessonId,
                Ordinal = step.Ordinal,
                Text = step.Text,
                AnchorKey = step.AnchorKey,
            };
        }

        /// <summary>Returns a rejection if any added/edited lesson's trigger is internally inconsistent: a
        /// <see cref="ELessonTriggerType.MechanicEvent"/> lesson must name a <see cref="EMechanicEvent"/>, and a
        /// <see cref="ELessonTriggerType.ScreenVisit"/> lesson must not carry a dangling one.</summary>
        private static AdminSaveResult? FindTriggerViolation(IReadOnlyList<Change<Contracts.Lesson>> changes)
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == EChangeType.Delete)
                {
                    continue;
                }

                var item = change.Item;
                if (item.TriggerType == ELessonTriggerType.MechanicEvent && item.TriggerMechanicEvent is null)
                {
                    return AdminSaveResult.Failure("A mechanic-event lesson must name a mechanic event.");
                }

                if (item.TriggerType == ELessonTriggerType.ScreenVisit && item.TriggerMechanicEvent is not null)
                {
                    return AdminSaveResult.Failure("A screen-visit lesson cannot also carry a mechanic-event trigger.");
                }
            }

            return null;
        }

        /// <summary>Returns a rejection if the prospective set of lessons (the cached lessons with each Edit
        /// replacing its record by Id, each Add appended, each Delete removed) would place two lessons at the
        /// same Key, else null. The lint matches lessons by Key, so a collision would make the match ambiguous.
        /// The DB unique index remains the backstop.</summary>
        private AdminSaveResult? FindKeyCollision(IReadOnlyList<Change<Contracts.Lesson>> changes)
        {
            var prospective = _lessons.AllLessonEntities().ToDictionary(l => l.Id, l => l.Key);

            foreach (var change in changes)
            {
                switch (change.ChangeType)
                {
                    case EChangeType.Delete:
                        prospective.Remove(change.Item.Id);
                        break;
                    case EChangeType.Edit:
                        prospective[change.Item.Id] = change.Item.Key;
                        break;
                    case EChangeType.Add:
                        // Adds carry an unassigned Id, so they can't key the dictionary; collected below.
                        break;
                }
            }

            var keys = prospective.Values
                .Concat(changes.Where(c => c.ChangeType == EChangeType.Add).Select(c => c.Item.Key));

            var seen = new HashSet<string>();
            foreach (var key in keys)
            {
                if (!seen.Add(key))
                {
                    return AdminSaveResult.Failure($"Two lessons would share the key '{key}'.");
                }
            }

            return null;
        }
    }
}
