namespace Game.Core.Tests.Battle.Performance
{
    /// <summary>
    /// Per-operation timing statistics produced by <see cref="PerformanceMeasurement"/>. All figures
    /// are microseconds for a single operation.
    /// </summary>
    /// <remarks>
    /// The ratio assertions gate on <see cref="MinMicroseconds"/>: timing noise can only ever add
    /// time, so the fastest observed sample is the cleanest, most reproducible estimate of the true
    /// cost. <see cref="MedianMicroseconds"/> and <see cref="MeanMicroseconds"/> are reported
    /// alongside it (via the test output) for human trend-watching.
    /// </remarks>
    public sealed record MeasurementResult(
        double MinMicroseconds,
        double MedianMicroseconds,
        double MeanMicroseconds,
        IReadOnlyList<double> SamplesMicroseconds)
    {
        public static MeasurementResult FromSamples(IReadOnlyList<double> samples)
        {
            if (samples.Count == 0)
            {
                throw new ArgumentException("At least one sample is required.", nameof(samples));
            }

            var sorted = samples.OrderBy(value => value).ToArray();
            var min = sorted[0];
            var mean = sorted.Average();

            var middle = sorted.Length / 2;
            var median = sorted.Length % 2 == 1
                ? sorted[middle]
                : (sorted[middle - 1] + sorted[middle]) / 2.0;

            return new MeasurementResult(min, median, mean, samples);
        }
    }
}
