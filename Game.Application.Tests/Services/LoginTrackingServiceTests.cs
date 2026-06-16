using Game.Abstractions.DataAccess;
using Game.Application;
using Game.Application.Services;
using Xunit;

namespace Game.Application.Tests.Services
{
    /// <summary>
    /// Unit tests for <see cref="LoginTrackingService"/>'s cancellation plumbing (#708): the connection-recording
    /// and device-info paths run in middleware/controllers outside the per-action commit pipeline, so they must
    /// forward the request's cancellation token to both the repository read/write and the unit-of-work commit.
    /// </summary>
    public class LoginTrackingServiceTests
    {
        [Fact]
        public async Task RecordConnection_ForwardsCancellationTokenToRepoAndCommit()
        {
            var userLogins = new FakeUserLogins();
            var unitOfWork = new FakeUnitOfWork();
            var service = new LoginTrackingService(userLogins, unitOfWork);
            using var cts = new CancellationTokenSource();

            await service.RecordConnection(5, "127.0.0.1", "fp", "ua", null, null, null, cts.Token);

            Assert.Equal(cts.Token, userLogins.LastRecordToken);
            Assert.Equal(cts.Token, unitOfWork.LastCommitToken);
        }

        [Fact]
        public async Task SaveDeviceInfo_ForwardsCancellationTokenToRepoAndCommit()
        {
            var userLogins = new FakeUserLogins();
            var unitOfWork = new FakeUnitOfWork();
            var service = new LoginTrackingService(userLogins, unitOfWork);
            using var cts = new CancellationTokenSource();

            await service.SaveDeviceInfo("fp", "ua", null, null, null, 8, 4, cts.Token);

            Assert.Equal(cts.Token, userLogins.LastSaveToken);
            Assert.Equal(cts.Token, unitOfWork.LastCommitToken);
        }

        private sealed class FakeUserLogins : IUserLogins
        {
            public CancellationToken LastRecordToken { get; private set; }
            public CancellationToken LastSaveToken { get; private set; }

            public Task RecordConnection(
                int userId,
                string ipAddress,
                string deviceFingerprintHash,
                string userAgent,
                string? secChUa,
                string? secChUaMobile,
                string? secChUaPlatform,
                CancellationToken cancellationToken = default)
            {
                LastRecordToken = cancellationToken;
                return Task.CompletedTask;
            }

            public Task SaveDeviceInfo(
                string deviceFingerprintHash,
                string userAgent,
                string? secChUa,
                string? secChUaMobile,
                string? secChUaPlatform,
                double? deviceMemory,
                int? hardwareConcurrency,
                CancellationToken cancellationToken = default)
            {
                LastSaveToken = cancellationToken;
                return Task.CompletedTask;
            }
        }

        private sealed class FakeUnitOfWork : IUnitOfWork
        {
            public CancellationToken LastCommitToken { get; private set; }

            public Task CommitAsync(CancellationToken cancellationToken = default)
            {
                LastCommitToken = cancellationToken;
                return Task.CompletedTask;
            }
        }
    }
}
