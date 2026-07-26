using System.Globalization;

namespace Game.Core.TestInfrastructure.Helpers
{
    /// <summary>
    /// Runs a test body under a chosen culture and restores the previous one on dispose, so behaviour
    /// that must be locale-independent (e.g. the codegen's byte-compared output, or a display name baked
    /// into seed data) can be pinned against a culture whose casing/collation differs from the invariant one.
    /// <see cref="CultureInfo.CurrentCulture"/> is thread-static, so a *synchronous* test body cannot
    /// leak its culture into a parallel test — do not await inside the scope, as the continuation may
    /// resume on another thread and leave the culture unrestored there.
    /// </summary>
    public sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previousCulture;

        public CultureScope(string cultureName)
        {
            _previousCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _previousCulture;
        }
    }
}
