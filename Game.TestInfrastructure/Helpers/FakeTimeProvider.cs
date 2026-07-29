namespace Game.TestInfrastructure.Helpers
{
    /// <summary>
    /// The canonical <see cref="TimeProvider"/> test double: a clock that only moves when a test moves it, so
    /// a deadline or grace period can be crossed deterministically instead of via wall-clock delays.
    /// </summary>
    /// <remarks>
    /// Hand-rolled rather than taken from <c>Microsoft.Extensions.TimeProvider.Testing</c>: no caller needs that
    /// package's timer support, and the dependency would buy nothing the four lines below don't already give.
    /// </remarks>
    public sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now += delta;
    }
}
