using Game.Core.Identity;
using Xunit;

namespace Game.Core.Tests.Identity
{
    public class AdminLockoutPolicyTests
    {
        [Theory]
        [InlineData(5, 5, true)]
        [InlineData(5, 6, false)]
        [InlineData(1, 0, false)]
        public void IsSelfTarget_ComparesActingAndTargetIds(int actingUserId, int targetUserId, bool expected)
        {
            Assert.Equal(expected, AdminLockoutPolicy.IsSelfTarget(actingUserId, targetUserId));
        }

        [Theory]
        // A grant or a change that keeps Admin never removes it, so it is always allowed.
        [InlineData(false, true, false)]  // target has no admin, request keeps admin
        [InlineData(true, true, false)]   // target keeps admin
        [InlineData(false, false, false)] // target has no admin, request drops admin (no-op)
        // Removing admin from another user is allowed while another admin remains.
        [InlineData(true, false, true)]
        public void CheckRoleChange_NonLockoutChanges_AreAllowed(
            bool targetHasAdminRole,
            bool requestedRolesIncludeAdmin,
            bool otherUsableAdminsRemain)
        {
            // Acting on a different user (1 vs 2) to isolate from the self-removal rule.
            var result = AdminLockoutPolicy.CheckRoleChange(
                actingUserId: 1,
                targetUserId: 2,
                targetHasAdminRole,
                requestedRolesIncludeAdmin,
                otherUsableAdminsRemain);

            Assert.Equal(RoleChangeProtection.Allowed, result);
        }

        [Fact]
        public void CheckRoleChange_StrippingOwnAdminRole_IsSelfAdminRemoval()
        {
            var result = AdminLockoutPolicy.CheckRoleChange(
                actingUserId: 7,
                targetUserId: 7,
                targetHasAdminRole: true,
                requestedRolesIncludeAdmin: false,
                otherUsableAdminsRemain: true);

            Assert.Equal(RoleChangeProtection.SelfAdminRemoval, result);
        }

        [Fact]
        public void CheckRoleChange_SelfRemovalTakesPrecedenceOverLastAdmin()
        {
            // Acting admin strips their own role and is the last admin: the self-removal rule wins.
            var result = AdminLockoutPolicy.CheckRoleChange(
                actingUserId: 7,
                targetUserId: 7,
                targetHasAdminRole: true,
                requestedRolesIncludeAdmin: false,
                otherUsableAdminsRemain: false);

            Assert.Equal(RoleChangeProtection.SelfAdminRemoval, result);
        }

        [Fact]
        public void CheckRoleChange_RemovingAdminFromLastAdmin_IsLastAdmin()
        {
            var result = AdminLockoutPolicy.CheckRoleChange(
                actingUserId: 1,
                targetUserId: 2,
                targetHasAdminRole: true,
                requestedRolesIncludeAdmin: false,
                otherUsableAdminsRemain: false);

            Assert.Equal(RoleChangeProtection.LastAdmin, result);
        }

        [Fact]
        public void CheckRoleChange_KeepingOwnAdminRole_IsAllowed()
        {
            // Editing your own roles is fine as long as Admin is retained.
            var result = AdminLockoutPolicy.CheckRoleChange(
                actingUserId: 7,
                targetUserId: 7,
                targetHasAdminRole: true,
                requestedRolesIncludeAdmin: true,
                otherUsableAdminsRemain: false);

            Assert.Equal(RoleChangeProtection.Allowed, result);
        }

        [Theory]
        // Self-target is rejected regardless of role or surviving-admin state.
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void CheckUserAction_TargetingSelf_IsSelfTarget(bool targetHasAdminRole, bool otherUsableAdminsRemain)
        {
            var result = AdminLockoutPolicy.CheckUserAction(
                actingUserId: 7,
                targetUserId: 7,
                targetHasAdminRole,
                otherUsableAdminsRemain);

            Assert.Equal(UserActionProtection.SelfTarget, result);
        }

        [Theory]
        // Acting on a non-admin never reduces the admin pool, so it is always allowed.
        [InlineData(true)]
        [InlineData(false)]
        public void CheckUserAction_NonAdminTarget_IsAllowed(bool otherUsableAdminsRemain)
        {
            var result = AdminLockoutPolicy.CheckUserAction(
                actingUserId: 1,
                targetUserId: 2,
                targetHasAdminRole: false,
                otherUsableAdminsRemain);

            Assert.Equal(UserActionProtection.Allowed, result);
        }

        [Fact]
        public void CheckUserAction_AdminTarget_WhileUsableAdminsRemain_IsAllowed()
        {
            var result = AdminLockoutPolicy.CheckUserAction(
                actingUserId: 1,
                targetUserId: 2,
                targetHasAdminRole: true,
                otherUsableAdminsRemain: true);

            Assert.Equal(UserActionProtection.Allowed, result);
        }

        [Fact]
        public void CheckUserAction_AdminTarget_WhenNoUsableAdminsRemain_IsLastAdmin()
        {
            var result = AdminLockoutPolicy.CheckUserAction(
                actingUserId: 1,
                targetUserId: 2,
                targetHasAdminRole: true,
                otherUsableAdminsRemain: false);

            Assert.Equal(UserActionProtection.LastAdmin, result);
        }
    }
}
