using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;

namespace Game.Abstractions.DataAccess.Admin
{
    /// <summary>
    /// Content Authoring persistence for tutorial lessons and their ordered tour steps. Encapsulates the EF
    /// specifics behind an entity-free admin contract surface.
    /// </summary>
    public interface IAdminLessons
    {
        /// <summary>Applies an identity-level Add/Edit change set to the lesson catalogue (retire-only — a
        /// Delete is rejected). Fails if an edit targets a lesson that does not exist, a mechanic-event lesson
        /// names no mechanic event (or vice versa), or the batch would leave two lessons sharing a Key.</summary>
        AdminSaveResult SaveLessons(IReadOnlyList<Change<Lesson>> changes);

        /// <summary>Reconciles a lesson's ordered tour steps. Fails if the lesson does not exist.</summary>
        AdminSaveResult SetSteps(SetLessonStepsData data);
    }
}
