namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// The full desired set of a lesson's tour steps, keyed by the owner's <see cref="Id"/>. Reconciled
    /// against the existing rows by <see cref="LessonStep.Ordinal"/>.
    /// </summary>
    public class SetLessonStepsData
    {
        public int Id { get; set; }
        public required List<LessonStep> Steps { get; set; }
    }
}
