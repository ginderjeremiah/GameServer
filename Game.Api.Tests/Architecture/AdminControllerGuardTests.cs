using System.Reflection;
using Game.Api.Controllers;
using Game.Api.Controllers.Admin;
using Game.Api.Filters;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Game.Api.Tests.Architecture
{
    /// <summary>
    /// Architecture guard for the admin surface (#1891). Both <see cref="AdminRoleAuthorizationFilter"/> and
    /// <see cref="ReloadReferenceCachesAttribute"/> are opt-in per controller rather than applied by convention,
    /// so a new (or moved) admin controller that forgets one silently exposes an admin action to any
    /// authenticated player, or serves stale reference data after a write. This enumerates every controller in
    /// the assembly rather than a fixed list, so a controller added later is checked automatically instead of
    /// only when someone remembers to add it here.
    /// </summary>
    public class AdminControllerGuardTests
    {
        /// <summary>
        /// Content-writing admin controllers documented as owning no reference cache to reload: user accounts
        /// and the write-behind dead-letter queues aren't list-cached in memory (see their own class remarks).
        /// </summary>
        private static readonly HashSet<Type> ReloadCacheExemptControllers =
        [
            typeof(AdminUsersController),
            typeof(AdminDeadLettersController),
        ];

        public static IEnumerable<object[]> AdminSurfaceControllers()
        {
            return typeof(Startup).Assembly
                .GetTypes()
                .Where(IsAdminSurfaceController)
                .Select(type => new object[] { type });
        }

        [Theory]
        [MemberData(nameof(AdminSurfaceControllers))]
        public void AdminSurfaceController_RequiresAdminRole(Type controllerType)
        {
            Assert.True(
                HasServiceFilter(controllerType, typeof(AdminRoleAuthorizationFilter)),
                $"{controllerType.Name} sits on the admin surface but is missing [ServiceFilter(typeof(AdminRoleAuthorizationFilter))].");
        }

        [Theory]
        [MemberData(nameof(AdminSurfaceControllers))]
        public void ContentWritingAdminController_ReloadsReferenceCaches(Type controllerType)
        {
            if (ReloadCacheExemptControllers.Contains(controllerType))
            {
                return;
            }

            var writesContent = controllerType
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Any(method => method.GetCustomAttributes(inherit: true).Any(IsWriteHttpMethodAttribute));

            if (!writesContent)
            {
                return;
            }

            Assert.True(
                controllerType.GetCustomAttributes(typeof(ReloadReferenceCachesAttribute), inherit: true).Length > 0,
                $"{controllerType.Name} writes content but is missing [ReloadReferenceCaches].");
        }

        private static bool IsAdminSurfaceController(Type type)
        {
            // Off-convention admin controllers (ones that don't route under /api/AdminTools) must be
            // special-cased here too, or they silently fall outside this guard's coverage.
            if (type == typeof(TagsController))
            {
                return true;
            }

            if (!typeof(ControllerBase).IsAssignableFrom(type) || type.IsAbstract)
            {
                return false;
            }

            var route = type.GetCustomAttribute<RouteAttribute>(inherit: true);
            return route?.Template.StartsWith("/api/AdminTools", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        private static bool HasServiceFilter(Type type, Type serviceType)
        {
            return type.GetCustomAttributes<ServiceFilterAttribute>(inherit: true)
                .Any(attribute => attribute.ServiceType == serviceType);
        }

        private static bool IsWriteHttpMethodAttribute(object attribute)
        {
            return attribute is HttpPostAttribute or HttpPutAttribute or HttpDeleteAttribute or HttpPatchAttribute;
        }
    }
}
