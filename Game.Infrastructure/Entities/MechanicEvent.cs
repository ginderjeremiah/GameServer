namespace Game.Infrastructure.Entities
{
    /// <summary>Intrinsic mechanic-event catalogue (#1591, spike #1392): the fixed set of content-events
    /// detectors (UI/src/lib/common/content-events.ts) a mechanic-anchored Lesson can trigger on.</summary>
    public class MechanicEvent
    {
        public int Id { get; set; }
        public required string Name { get; set; }
    }
}
