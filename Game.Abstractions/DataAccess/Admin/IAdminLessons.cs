using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;

namespace Game.Abstractions.DataAccess.Admin
{
    /// <summary>
    /// Content Authoring persistence for tutorial lessons and their step list (#1591, spike #1392).
    /// Encapsulates the EF specifics behind an entity-free admin contract surface.
    /// </summary>
    public interface IAdminLessons
    {
        /// <summary>Identity Add/Edit (retire-only). Rejects: duplicate Key among live lessons; ScreenVisit
        /// trigger missing TriggerScreenKey or carrying a TriggerMechanicEvent; MechanicEvent trigger missing/
        /// invalid TriggerMechanicEvent or carrying a TriggerScreenKey; empty HostScreenKey.</summary>
        AdminSaveResult SaveLessons(IReadOnlyList<Change<Lesson>> changes);

        /// <summary>Replaces a lesson's ordered step list wholesale (delete-all-then-insert, since steps have no
        /// independent identity worth diffing — order is the submitted array position). Rejects an empty list or
        /// a step with empty Text.</summary>
        AdminSaveResult SetSteps(SetLessonStepsData data);
    }
}
