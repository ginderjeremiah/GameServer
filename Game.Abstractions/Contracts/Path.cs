using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>Read/authoring contract for a path: an ordered sequence of proficiencies (its tiers) and the
    /// single <see cref="ActivityKey"/> the path trains on. The tiers are <see cref="Proficiency"/> records
    /// carrying this path's id.</summary>
    public class Path : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }

        /// <summary>The activity this path trains on — a damage-type key, category, or combat event (see the
        /// entity). A battle quantity whose key resolves to this trains the path's frontier tier.</summary>
        public EActivityKey ActivityKey { get; set; }

        /// <summary>Authoring-only design rationale (why this piece exists) — surfaced in the Workbench and
        /// version-controlled via the content export. The battle never reads it and the client never renders it.</summary>
        public required string DesignerNotes { get; set; }

        /// <summary>When set, the record is retired (out of circulation but kept resolvable by id).</summary>
        public DateTime? RetiredAt { get; set; }
    }
}
