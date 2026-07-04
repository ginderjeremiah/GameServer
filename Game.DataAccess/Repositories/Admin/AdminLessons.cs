using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Content Authoring persistence for tutorial lessons and their step list (#1591, spike #1392). Reuses the
    /// cached entity lookups for existence/diff and builds fresh, navigation-free entities for every write. The
    /// identity save is retire-only (no hard delete); the step list is reconciled as a wholesale
    /// delete-all-then-insert (steps have no independent identity worth diffing). <see cref="EMechanicEvent"/> is
    /// an intrinsic, migration-seeded enum, so its membership is validated structurally rather than through a
    /// dedicated reference cache.
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

            if (FindDuplicateKeyViolation(changes) is { } duplicateKeyRejection)
            {
                return duplicateKeyRejection;
            }

            return ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.Lesson
                {
                    Key = item.Key,
                    Name = item.Name,
                    TriggerType = item.TriggerType,
                    TriggerScreenKey = item.TriggerScreenKey,
                    TriggerMechanicEventId = item.TriggerMechanicEvent is { } evt ? (int)evt : null,
                    HostScreenKey = item.HostScreenKey,
                    DisplayOrder = item.DisplayOrder,
                }),
                edit: item => _entityStore.Update(new Entities.Lesson
                {
                    Id = item.Id,
                    Key = item.Key,
                    Name = item.Name,
                    TriggerType = item.TriggerType,
                    TriggerScreenKey = item.TriggerScreenKey,
                    TriggerMechanicEventId = item.TriggerMechanicEvent is { } evt ? (int)evt : null,
                    HostScreenKey = item.HostScreenKey,
                    DisplayOrder = item.DisplayOrder,
                    RetiredAt = item.RetiredAt,
                }),
                key: item => item.Id,
                resourceName: "lesson",
                editExists: item => _lessons.LookupLesson(item.Id) is not null);
        }

        public AdminSaveResult SetSteps(SetLessonStepsData data)
        {
            var lesson = _lessons.LookupLesson(data.Id);
            if (lesson is null)
            {
                return AdminSaveResult.NotFound("Lesson");
            }

            // A lesson's tour needs at least one step to show; an empty list would be a no-op tour.
            if (data.Steps.Count == 0)
            {
                return AdminSaveResult.Failure("A lesson must have at least one step.");
            }

            foreach (var step in data.Steps)
            {
                if (string.IsNullOrWhiteSpace(step.Text))
                {
                    return AdminSaveResult.Failure("A lesson step must have non-empty Text.");
                }
            }

            // Steps have no independent identity worth diffing (order is the submitted array position), so the
            // whole list is replaced wholesale rather than reconciled key-by-key.
            foreach (var existing in lesson.Steps)
            {
                _entityStore.DeleteByKey<Entities.LessonStep>(existing.Id);
            }

            for (var i = 0; i < data.Steps.Count; i++)
            {
                var step = data.Steps[i];
                _entityStore.Insert(new Entities.LessonStep
                {
                    LessonId = lesson.Id,
                    Order = i,
                    Text = step.Text,
                    AnchorKey = step.AnchorKey,
                });
            }

            return AdminSaveResult.Success;
        }

        /// <summary>Returns a rejection if any added/edited lesson's trigger declaration is inconsistent with its
        /// declared <see cref="ELessonTriggerType"/>, or its HostScreenKey is empty, else null.</summary>
        private static AdminSaveResult? FindTriggerViolation(IReadOnlyList<Change<Contracts.Lesson>> changes)
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == EChangeType.Delete)
                {
                    continue;
                }

                var item = change.Item;
                if (string.IsNullOrWhiteSpace(item.HostScreenKey))
                {
                    return AdminSaveResult.Failure("A lesson must declare a non-empty HostScreenKey.");
                }

                switch (item.TriggerType)
                {
                    case ELessonTriggerType.ScreenVisit:
                        if (string.IsNullOrWhiteSpace(item.TriggerScreenKey))
                        {
                            return AdminSaveResult.Failure("A ScreenVisit-triggered lesson must declare a TriggerScreenKey.");
                        }

                        if (item.TriggerMechanicEvent is not null)
                        {
                            return AdminSaveResult.Failure("A ScreenVisit-triggered lesson must not declare a TriggerMechanicEvent.");
                        }

                        break;
                    case ELessonTriggerType.MechanicEvent:
                        if (item.TriggerMechanicEvent is not { } mechanicEvent || !Enum.IsDefined(mechanicEvent))
                        {
                            return AdminSaveResult.Failure("A MechanicEvent-triggered lesson must declare a valid TriggerMechanicEvent.");
                        }

                        if (item.TriggerScreenKey is not null)
                        {
                            return AdminSaveResult.Failure("A MechanicEvent-triggered lesson must not declare a TriggerScreenKey.");
                        }

                        break;
                }
            }

            return null;
        }

        /// <summary>Returns a rejection if the batch would leave two live (non-retired) lessons sharing the same
        /// <see cref="Contracts.Lesson.Key"/>, else null. Builds the prospective live-key map — live lessons with
        /// each Edit re-keying/retiring its lesson and each Add contributing a new key — then flags a key claimed
        /// more than once. The DB's unique index on Key is the structural backstop; this is the clean rejection.</summary>
        private AdminSaveResult? FindDuplicateKeyViolation(IReadOnlyList<Change<Contracts.Lesson>> changes)
        {
            var keyByLesson = _lessons.AllLessonEntities()
                .Where(l => l.RetiredAt is null)
                .ToDictionary(l => l.Id, l => l.Key);

            // An Add's real id is store-generated and unknown here, so a descending sentinel keeps each new
            // lesson distinct from every real lesson id and from the other Adds in the batch.
            var addKey = 0;
            foreach (var change in changes)
            {
                if (change.ChangeType == EChangeType.Add)
                {
                    keyByLesson[--addKey] = change.Item.Key;
                }
                else if (change.ChangeType == EChangeType.Edit)
                {
                    if (change.Item.RetiredAt is not null)
                    {
                        keyByLesson.Remove(change.Item.Id);
                    }
                    else
                    {
                        keyByLesson[change.Item.Id] = change.Item.Key;
                    }
                }
            }

            var duplicate = keyByLesson.Values.GroupBy(k => k, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
            if (duplicate is null)
            {
                return null;
            }

            return AdminSaveResult.Failure($"Lesson key '{duplicate.Key}' is already used by another live lesson.");
        }
    }
}
