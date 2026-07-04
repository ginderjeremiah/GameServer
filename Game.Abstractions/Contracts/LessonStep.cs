namespace Game.Abstractions.Contracts
{
    /// <summary>Read/authoring contract for one step of a lesson's coach-mark tour.</summary>
    public class LessonStep : IModel
    {
        /// <summary>Position within the lesson's tour (0-based).</summary>
        public int Ordinal { get; set; }

        /// <summary>The callout text shown for this step.</summary>
        public required string Text { get; set; }

        /// <summary>The frontend anchor key this step points at (<c>use:tutorialAnchor</c>, #1592), or null for
        /// a centered, unanchored callout.</summary>
        public string? AnchorKey { get; set; }
    }
}
