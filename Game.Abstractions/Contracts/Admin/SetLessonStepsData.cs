namespace Game.Abstractions.Contracts.Admin
{
    /// <summary>
    /// The full desired ordered step list for a lesson, keyed by the owner's <see cref="Id"/>. Replaces the
    /// lesson's whole step list wholesale — array position is the order.
    /// </summary>
    public class SetLessonStepsData
    {
        public int Id { get; set; }
        public required List<LessonStep> Steps { get; set; }
    }
}
