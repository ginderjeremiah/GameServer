namespace Game.Core.Classes
{
    /// <summary>
    /// A class's signature passive — the durable combat-identity bonus fed through the attribute pipeline
    /// (see the class-system spike, <c>docs/spikes/1126-class-system.md</c>). Flat or attribute-scaled
    /// (<c>Amount + ScalingAttribute × ScalingAmount</c>), mirroring skill-effect scaling. Structurally
    /// immutable so the shared cached <see cref="Class"/> graph can be reused by reference. The assembly of
    /// this into the battler lands in a later sub-issue (#1224).
    /// </summary>
    public sealed class ClassSignaturePassive
    {
        public required EAttribute Attribute { get; init; }
        public required decimal Amount { get; init; }
        public EAttribute? ScalingAttribute { get; init; }
        public required decimal ScalingAmount { get; init; }
        public required EModifierType ModifierType { get; init; }
    }
}
