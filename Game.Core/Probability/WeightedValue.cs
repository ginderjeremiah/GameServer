namespace Game.Core.Probability
{
    /// <summary>
    /// A value with an associated probability weight.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="Value">The value to associate a probabilistic weight with.</param>
    /// <param name="Weight">The probabilistic weight of choosing the associated value.</param>
    public record WeightedValue<T>(T Value, int Weight);
}
