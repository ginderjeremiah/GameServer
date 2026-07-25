namespace Game.Core.Battle
{
    /// <summary>
    /// The shared math behind the overlay training tallies (#1481). Every overlay signal — Precision, Hex,
    /// Momentum, Sunder, Cull and the Frequency pseudo-overlay — books the same share claim: the hit's booked
    /// (health-capped) damage × <c>φ</c> of that overlay's own investment. Only the investment differs, so the
    /// saturation lives here rather than on any one battler or its effect stacks: half its callers (the crit,
    /// execute and cadence investments) read no applied effect at all.
    /// </summary>
    public static class OverlayTally
    {
        /// <summary>
        /// The shared overlay-tally saturation <c>φ(a) = a / (1 + a)</c>, applied to an overlay's own investment
        /// magnitude when booking its share claim: ~linear at the low end so a token investment trains
        /// proportionally little, asymptoting to <c>1</c> so even a huge investment claims at most the full booked
        /// hit. Callers guard their own non-positive investments (#1927) — <c>φ</c> has a pole at <c>a = −1</c>.
        /// </summary>
        public static double NormalizeInvestment(double investment)
        {
            return investment / (1.0 + investment);
        }
    }
}
