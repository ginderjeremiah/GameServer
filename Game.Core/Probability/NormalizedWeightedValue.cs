namespace Game.Core.Probability
{
    internal class NormalizedWeightedValue<T>(T value, double normalizedWeight)
    {
        public T Value { get; set; } = value;
        public double NormalizedWeight { get; set; } = normalizedWeight;
    }
}
