using Microsoft.AspNetCore.Mvc;

namespace Game.Api.Filters
{
    /// <summary>
    /// Applies <see cref="AdminCacheReloadFilter"/> with its required outermost order baked in, so an admin
    /// controller reloads the reference caches after a successful write without having to remember the
    /// ordering. The order must be set on the filter metadata (the <see cref="ServiceFilterAttribute"/>),
    /// not the resolved instance, so a bare <c>[ServiceFilter(typeof(AdminCacheReloadFilter))]</c> would
    /// silently default to order 0 and reload before <see cref="CommitFilter"/> commits — this attribute
    /// removes that foot-gun.
    /// </summary>
    public sealed class ReloadReferenceCachesAttribute : ServiceFilterAttribute
    {
        public ReloadReferenceCachesAttribute() : base(typeof(AdminCacheReloadFilter))
        {
            Order = AdminCacheReloadFilter.FilterOrder;
        }
    }
}
