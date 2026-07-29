using Game.Application.Services;
using Game.TestInfrastructure.Helpers;
using Xunit;

namespace Game.Application.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="LoginTrackingService"/>'s cancellation plumbing (#708): the connection-recording
    /// and device-info paths run in middleware/controllers outside the per-action commit pipeline, so they must
    /// forward the request's cancellation token to the data tier — which owns the conflict-tolerant save (#907).
    /// </summary>
    public class LoginTrackingServiceTests
    {
        [Fact]
        public async Task RecordConnection_ForwardsCancellationTokenToRepo()
        {
            var userLogins = new FakeUserLogins();
            var service = new LoginTrackingService(userLogins);
            using var cts = new CancellationTokenSource();

            await service.RecordConnection(5, "127.0.0.1", "fp", "ua", null, null, null, cts.Token);

            Assert.Equal(cts.Token, userLogins.LastRecordToken);
        }

        [Fact]
        public async Task SaveDeviceInfo_ForwardsCancellationTokenToRepo()
        {
            var userLogins = new FakeUserLogins();
            var service = new LoginTrackingService(userLogins);
            using var cts = new CancellationTokenSource();

            await service.SaveDeviceInfo(5, "fp", null, null, null, 8, 4, cts.Token);

            Assert.Equal(cts.Token, userLogins.LastSaveToken);
        }
    }
}
