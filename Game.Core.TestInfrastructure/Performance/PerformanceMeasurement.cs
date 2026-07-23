using System.Diagnostics;

namespace Game.Core.TestInfrastructure.Performance
{
    /// <summary>
    /// A small, dependency-free timing harness shared by this repo's performance test suites. Its only
    /// job is to make per-operation timing as <em>stable and reproducible</em> as possible on noisy,
    /// shared CI hardware, so ratio-based or ceiling assertions built on it do not flake:
    /// <list type="bullet">
    ///   <item>A warm-up phase runs the workload untimed so JIT compilation and cache warming never
    ///   land inside a measured sample.</item>
    ///   <item>Each sample times a batch of <c>operationsPerSample</c> invocations and divides, so one
    ///   coarse <see cref="Stopwatch"/> read amortises over many fast operations.</item>
    ///   <item>The measured work is separated from its setup: a fresh input is built by
    ///   <c>createInput</c> for every operation <em>outside</em> the timed region (the subject may be
    ///   stateful and unsafe to reuse across operations), so the figures reflect the operation itself,
    ///   not its input's construction.</item>
    ///   <item>The GC is settled after the (allocating) setup and before timing, and the reported
    ///   gate figure is <see cref="MeasurementResult.MinMicroseconds"/> across samples — timing noise
    ///   (scheduling, GC, contention) can only ever <em>add</em> time, so the fastest observed sample is
    ///   the cleanest estimate of the true cost and the most reproducible statistic to compare against.</item>
    /// </list>
    /// </summary>
    public static class PerformanceMeasurement
    {
        /// <summary>
        /// Measures the per-operation cost of <paramref name="timedOperation"/>, building a fresh
        /// input for each operation via <paramref name="createInput"/> outside the timed region.
        /// </summary>
        /// <typeparam name="TInput">The per-operation input type (e.g. a pre-built simulator).</typeparam>
        /// <param name="createInput">Builds one fresh input. Invoked untimed, once per operation.</param>
        /// <param name="timedOperation">The work being measured, applied to a fresh input.</param>
        /// <param name="warmupIterations">Untimed build+run iterations performed before sampling.</param>
        /// <param name="sampleCount">Number of timed samples; the result aggregates across them.</param>
        /// <param name="operationsPerSample">Operations batched into each timed sample.</param>
        public static MeasurementResult Measure<TInput>(
            Func<TInput> createInput,
            Action<TInput> timedOperation,
            int warmupIterations,
            int sampleCount,
            int operationsPerSample)
        {
            ArgumentNullException.ThrowIfNull(createInput);
            ArgumentNullException.ThrowIfNull(timedOperation);
            ArgumentOutOfRangeException.ThrowIfNegative(warmupIterations);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleCount);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(operationsPerSample);

            for (var i = 0; i < warmupIterations; i++)
            {
                timedOperation(createInput());
            }

            var inputs = new TInput[operationsPerSample];
            var perOperationMicroseconds = new double[sampleCount];

            for (var sample = 0; sample < sampleCount; sample++)
            {
                // Build all inputs for this sample untimed, so input construction (and the garbage it
                // produces) stays out of the measured region.
                for (var i = 0; i < operationsPerSample; i++)
                {
                    inputs[i] = createInput();
                }

                Settle();

                var stopwatch = Stopwatch.StartNew();
                for (var i = 0; i < operationsPerSample; i++)
                {
                    timedOperation(inputs[i]);
                }
                stopwatch.Stop();

                perOperationMicroseconds[sample] = stopwatch.Elapsed.TotalMicroseconds / operationsPerSample;
            }

            return MeasurementResult.FromSamples(perOperationMicroseconds);
        }

        /// <summary>
        /// Async counterpart of <see cref="Measure{TInput}"/> for I/O-bound operations (e.g. a real
        /// Postgres/Redis round trip) that cannot be timed synchronously. Semantics are otherwise
        /// identical, including building every sample's inputs untimed before the batch is awaited in turn.
        /// </summary>
        public static async Task<MeasurementResult> MeasureAsync<TInput>(
            Func<Task<TInput>> createInput,
            Func<TInput, Task> timedOperation,
            int warmupIterations,
            int sampleCount,
            int operationsPerSample)
        {
            ArgumentNullException.ThrowIfNull(createInput);
            ArgumentNullException.ThrowIfNull(timedOperation);
            ArgumentOutOfRangeException.ThrowIfNegative(warmupIterations);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleCount);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(operationsPerSample);

            for (var i = 0; i < warmupIterations; i++)
            {
                await timedOperation(await createInput());
            }

            var inputs = new TInput[operationsPerSample];
            var perOperationMicroseconds = new double[sampleCount];

            for (var sample = 0; sample < sampleCount; sample++)
            {
                for (var i = 0; i < operationsPerSample; i++)
                {
                    inputs[i] = await createInput();
                }

                Settle();

                var stopwatch = Stopwatch.StartNew();
                for (var i = 0; i < operationsPerSample; i++)
                {
                    await timedOperation(inputs[i]);
                }
                stopwatch.Stop();

                perOperationMicroseconds[sample] = stopwatch.Elapsed.TotalMicroseconds / operationsPerSample;
            }

            return MeasurementResult.FromSamples(perOperationMicroseconds);
        }

        /// <summary>
        /// Forces a full GC so a sample starts from a clean heap, reducing the chance that a
        /// collection triggered by the untimed setup lands inside the following timed region.
        /// </summary>
        private static void Settle()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
