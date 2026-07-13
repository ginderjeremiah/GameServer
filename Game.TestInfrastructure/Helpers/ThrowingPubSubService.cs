using Game.Abstractions.Infrastructure;

namespace Game.TestInfrastructure.Helpers
{
    /// <summary>
    /// Stands in for a transient Redis blip on a write-behind flush: <see cref="PublishBatch{T}"/> always throws,
    /// so a repository built with this fake exercises the "publish itself fails" path of <c>FlushAsync</c>.
    /// </summary>
    public sealed class ThrowingPubSubService : NotSupportedPubSubService
    {
        public override Task PublishBatch<T>(string channel, string queueName, IEnumerable<T> queueData, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated transient publish failure.");
    }
}
