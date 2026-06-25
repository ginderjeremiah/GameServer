using Game.Abstractions.Contracts;
using Game.Abstractions.Contracts.Admin;

namespace Game.Abstractions.DataAccess.Admin
{
    /// <summary>
    /// Content Authoring persistence for classes and their related collections (starter skills, starter
    /// equipment, attribute distributions). Encapsulates the EF specifics — fresh, navigation-free entities,
    /// Delete→Edit→Add ordering, and zero-based-id update semantics — behind an entity-free admin contract
    /// surface so <c>Game.Api</c> never sees the persistence shape. The identity save is retire-only (no
    /// hard delete); the relationship setters reconcile a full desired set.
    /// </summary>
    public interface IAdminClasses
    {
        /// <summary>Applies an identity-level Add/Edit change set to the class catalogue (including the
        /// signature-passive scalar fields). Fails (applying nothing) if an edit targets a class that does
        /// not exist.</summary>
        AdminSaveResult SaveClasses(IReadOnlyList<Change<Class>> changes);

        /// <summary>Replaces a class's starter skills. Fails if the class does not exist.</summary>
        AdminSaveResult SetStarterSkills(SetClassStarterSkillsData data);

        /// <summary>Replaces a class's starter equipment. Fails if the class does not exist.</summary>
        AdminSaveResult SetStarterEquipment(SetClassStarterEquipmentData data);

        /// <summary>Replaces a class's attribute distributions. Fails if the class does not exist.</summary>
        AdminSaveResult SetAttributeDistributions(SetClassAttributeDistributionsData data);
    }
}
