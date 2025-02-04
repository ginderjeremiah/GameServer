using Game.Abstractions.Infrastructure;

namespace Game.Infrastructure.PubSub
{
    /// <summary>
    /// Configuration options for an <see cref="IPubSubService"/>.
    /// </summary>
    public interface IPubSubOptions
    {
        /// <summary>
        /// Determines which underlying <see cref="IPubSubService"/> implementation to use.
        /// </summary>
        public PubSubSystem PubSubSystem { get; }

        /// <summary>
        /// The connection string used to configure the <see cref="IPubSubService"/>.
        /// </summary>
        public string? PubSubConnectionString { get; }
    }
}
