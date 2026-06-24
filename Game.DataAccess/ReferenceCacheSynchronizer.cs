using Game.Abstractions.DataAccess;
using Game.Abstractions.Infrastructure;
using Microsoft.Extensions.Hosting;

namespace Game.DataAccess
{
    /// <summary>
    /// Keeps the in-memory reference-data caches in sync across API instances (#359). After a successful
    /// admin write the serving instance broadcasts a reference-data-changed notification over Redis pub/sub
    /// (<see cref="NotifyChangedAsync"/>, called by the admin cache-reload filter alongside its own awaited
    /// reload); every instance subscribes at startup and reacts to a foreign notification with a debounced
    /// background reload-and-swap via <see cref="CoalescingReferenceCacheReloader"/>, so readers everywhere
    /// converge on the new data without ever being blocked. The message payload is the publisher's
    /// <see cref="InstanceId"/>, letting the publishing instance skip its own notification — its caches
    /// were already reloaded synchronously in the filter.
    /// </summary>
    internal sealed class ReferenceCacheSynchronizer : IHostedService, IReferenceDataChangeNotifier, IDisposable
    {
        private readonly IPubSubService _pubsub;
        private readonly CoalescingReferenceCacheReloader _reloader;
        private readonly CancellationTokenSource _stopping = new();
        private Task _reloadLoop = Task.CompletedTask;
        private bool _disposed;

        /// <summary>Identifies this process on the notification payload so it can skip its own broadcasts.</summary>
        internal string InstanceId { get; } = Guid.NewGuid().ToString();

        public ReferenceCacheSynchronizer(IPubSubService pubsub, CoalescingReferenceCacheReloader reloader)
        {
            _pubsub = pubsub;
            _reloader = reloader;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // The reload loop starts before the subscription so a notification can never find it missing.
            // The subscription is id-scoped (InstanceId) so StopAsync removes only this handler rather
            // than tearing down every subscriber the channel might gain.
            _reloadLoop = _reloader.RunAsync(_stopping.Token);
            await _pubsub.Subscribe(Constants.PUBSUB_REFERENCE_DATA_CHANNEL, HandleNotification, InstanceId);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _pubsub.UnSubscribe(Constants.PUBSUB_REFERENCE_DATA_CHANNEL, InstanceId);
            _stopping.Cancel();
            await _reloadLoop;
        }

        public async Task NotifyChangedAsync()
        {
            await _pubsub.Publish(Constants.PUBSUB_REFERENCE_DATA_CHANNEL, InstanceId);
        }

        // Runs on the pub/sub callback thread, so it only flips the reloader's signal and returns.
        private void HandleNotification((string message, string channel) args)
        {
            if (args.message == InstanceId)
            {
                // This instance published the change and already reloaded synchronously in the admin filter.
                return;
            }

            _reloader.NotifyChanged();
        }

        public void Dispose()
        {
            // Idempotent: the container captures this singleton for disposal once per registration
            // (it is also registered as IReferenceDataChangeNotifier), so Dispose runs more than once.
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stopping.Cancel();

            // Dispose the token source only once the reload loop it feeds has finished. The host always calls
            // StopAsync (which awaits the loop) before Dispose, so this normally disposes immediately — but a
            // Dispose without a preceding StopAsync would otherwise dispose the source out from under a loop
            // still observing the (now-cancelled) token, an ObjectDisposedException race. Deferring to the
            // loop's completion closes that window while still guaranteeing eventual disposal.
            _reloadLoop.ContinueWith(_ => _stopping.Dispose(), TaskScheduler.Default);
        }
    }
}
