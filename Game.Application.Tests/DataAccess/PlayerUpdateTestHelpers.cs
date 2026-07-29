using Game.Core.Players.Events;
using Game.DataAccess;
using Game.DataAccess.PlayerUpdates;
using Game.Infrastructure.Database;
using Game.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Shared fixtures for exercising the write-behind player-update handlers directly, rather than through a
    /// live drain. Both the guard's behavioural suite and its performance suite drive handlers the same way —
    /// a fresh scope per apply with the envelope's sequence published onto it — so the pieces that encode
    /// <em>how</em> the drain does that live here instead of once per suite.
    /// <para>
    /// This lives in the test project rather than <c>Game.TestInfrastructure</c> because it speaks
    /// <c>Game.DataAccess</c> internals (<see cref="DomainEventEnvelope"/>, <see cref="PlayerUpdateContext"/>),
    /// and this assembly is the only one that project grants <c>InternalsVisibleTo</c>.
    /// </para>
    /// </summary>
    internal static class PlayerUpdateTestHelpers
    {
        /// <summary>
        /// Publishes the envelope's sequence onto the scope exactly as <c>PlayerUpdateEventDispatcher</c> does;
        /// the payload itself is irrelevant when the handler is invoked directly with a typed event.
        /// </summary>
        /// <remarks>
        /// Deliberately one copy: it encodes the dispatcher's contract for populating
        /// <see cref="PlayerUpdateContext"/>, so a second field added there has one place to follow rather than
        /// one per suite — and a perf suite left silently mis-measuring is a quieter failure than a red test.
        /// </remarks>
        public static void DescribeSequence(IServiceScope scope, long sequence)
        {
            scope.ServiceProvider.GetRequiredService<PlayerUpdateContext>().Describe(new DomainEventEnvelope
            {
                Type = "test",
                Payload = "{}",
                Sequence = sequence,
            });
        }

        /// <summary>
        /// A <see cref="PlayerCoreUpdatedEvent"/> carrying the fields a test actually varies, with the rest
        /// held at fixed defaults so two suites can't drift on what "an unremarkable core update" means.
        /// </summary>
        public static PlayerCoreUpdatedEvent CoreEvent(int playerId, int level, int exp)
            => new(playerId, level, exp, 0, 100, 100, DateTime.UtcNow, false, null);

        /// <summary>
        /// Reads one watermark row's stored sequence, or <see langword="null"/> when the guard has never
        /// advanced that target.
        /// </summary>
        public static async Task<long?> ReadWatermarkAsync(
            IServiceScope scope,
            int playerId,
            PlayerWriteStream stream,
            string targetKey,
            CancellationToken cancellationToken)
        {
            var context = scope.ServiceProvider.GetRequiredService<GameContext>();
            var row = await context.PlayerWriteWatermarks.AsNoTracking()
                .SingleOrDefaultAsync(
                    w => w.PlayerId == playerId && w.Stream == stream && w.TargetKey == targetKey,
                    cancellationToken);
            return row?.LastAppliedSequence;
        }
    }
}
