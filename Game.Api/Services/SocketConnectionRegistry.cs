using Game.Api.Sockets;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

namespace Game.Api.Services
{
    /// <summary>
    /// Tracks every live player socket on this instance and drains them gracefully when the host stops
    /// (#526). Because <see cref="SocketManagerService"/> is transient (one instance per handshake), the
    /// registry is the single long-lived owner of the connection set, and as an <see cref="IHostedService"/>
    /// it ties into the host lifetime.
    ///
    /// On shutdown the per-socket read loops would otherwise block indefinitely on <c>ReceiveAsync</c>,
    /// leaving connections to be force-killed when the process dies — exactly the session churn the
    /// single-active-connection model is careful about. Draining instead closes each socket cleanly so the
    /// client reconnects to a healthy instance. The drain is kicked off from
    /// <see cref="IHostApplicationLifetime.ApplicationStopping"/> (which fires before the server begins
    /// waiting on the parked WebSocket requests, so closing the sockets is what lets those requests — and the
    /// host — finish), and <see cref="StopAsync"/> awaits its bounded completion.
    ///
    /// The drain runs in two phases via two cancellation tokens threaded into each handler's loops:
    /// <list type="number">
    /// <item><see cref="_stoppingCts"/> — cancelled first, stopping the inactivity watchdogs (their job is
    /// moot once the drain owns teardown).</item>
    /// <item><see cref="_drainCts"/> — cancelled only if the bounded <see cref="_drainTimeout"/> elapses,
    /// aborting any receive still blocked on a client that never completed the closing handshake so the
    /// loops (and the host) can finish stopping rather than waiting out the host's shutdown timeout.</item>
    /// </list>
    /// </summary>
    public sealed class SocketConnectionRegistry : IHostedService, IDisposable
    {
        /// <summary>
        /// How long the graceful drain waits for sockets to complete their closing handshake before
        /// aborting the stragglers. Sized well under the host's default 30s shutdown timeout so the abort
        /// path runs (and is observable) rather than the host force-killing the process first.
        /// </summary>
        private static readonly TimeSpan DefaultDrainTimeout = TimeSpan.FromSeconds(5);

        private readonly ConcurrentDictionary<string, SocketHandler> _handlers = new();
        private readonly CancellationTokenSource _stoppingCts = new();
        private readonly CancellationTokenSource _drainCts = new();
        private readonly IHostApplicationLifetime _lifetime;
        private readonly TimeSpan _drainTimeout;
        private readonly ILogger<SocketConnectionRegistry> _logger;

        private readonly object _drainLock = new();
        private Task? _drainTask;

        // Set once (under _drainLock, together with the drain snapshot) the instant draining begins. It gates
        // Register so a socket whose handshake completes after the snapshot is drained on the spot rather than
        // being tracked-but-undrained — see Register and DrainAsync (#904).
        private bool _draining;

        public SocketConnectionRegistry(IHostApplicationLifetime lifetime, ILogger<SocketConnectionRegistry> logger, TimeSpan? drainTimeout = null)
        {
            _lifetime = lifetime;
            _logger = logger;
            _drainTimeout = drainTimeout ?? DefaultDrainTimeout;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Begin draining the instant graceful shutdown starts — before the server waits on the parked
            // WebSocket requests — so closing the sockets is what unblocks them. StopAsync then awaits it.
            _lifetime.ApplicationStopping.Register(() => EnsureDraining());
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) => EnsureDraining();

        /// <summary>
        /// Registers a freshly-accepted socket and starts its loops, wiring the shutdown tokens so a drain
        /// can tear it down. Tracking and starting the socket happen under <see cref="_drainLock"/> so a
        /// concurrent <see cref="DrainAsync"/> snapshot can never catch a half-registered handler whose
        /// <see cref="SocketHandler.Listen"/> hasn't wired its shutdown tokens yet.
        ///
        /// A handshake that completes <b>after</b> the drain snapshot (the <see cref="_draining"/> gate is
        /// closed) would otherwise be tracked-but-undrained: never sent a close frame, its inactivity
        /// watchdog exiting at once on the already-cancelled <see cref="_stoppingCts"/>, and its receive
        /// blocking until the host force-kills the process. Such a latecomer is instead drained on the spot —
        /// returned so the caller awaits its clean close — so no socket slips past the drain (#904).
        /// </summary>
        public Task Register(SocketHandler handler)
        {
            lock (_drainLock)
            {
                if (!_draining)
                {
                    _handlers[handler.Id] = handler;
                    handler.Listen(_stoppingCts.Token, _drainCts.Token);
                    return Task.CompletedTask;
                }
            }

            return DrainLateSocketAsync(handler);
        }

        /// <summary>Removes a socket from the live set as it tears down (a clean close, replacement, etc.).</summary>
        public void Unregister(string socketId)
        {
            _handlers.TryRemove(socketId, out _);
        }

        /// <summary>
        /// Gracefully closes every live socket, bounded by <see cref="_drainTimeout"/>. Exposed (rather than
        /// only reachable through the lifetime hook) so the drain can be exercised deterministically in tests
        /// without standing up a real host shutdown.
        /// </summary>
        public async Task DrainAsync()
        {
            // Stop the inactivity watchdogs first — the drain owns teardown from here, so a concurrent
            // Inactivity close would just race the ServerShuttingDown close below.
            _stoppingCts.Cancel();

            SocketHandler[] handlers;
            lock (_drainLock)
            {
                // Close the gate and snapshot atomically: from here every socket is either in this snapshot
                // or will observe _draining in Register and drain itself — none is silently leaked between
                // the two (#904).
                _draining = true;
                handlers = _handlers.Values.ToArray();
            }

            if (handlers.Length == 0)
            {
                return;
            }

            _logger.LogInformation("Gracefully draining {Count} live socket(s) on shutdown.", handlers.Length);

            var drain = Task.WhenAll(handlers.Select(DrainHandlerAsync));
            try
            {
                await drain.WaitAsync(_drainTimeout);
                _logger.LogInformation("All sockets drained gracefully on shutdown.");
            }
            catch (TimeoutException)
            {
                // A client never completed the closing handshake within the window; abort the stuck receives
                // so the loops unwind. The per-handler tasks already swallow their errors, so awaiting the
                // (now promptly-completing) drain can't throw.
                _logger.LogWarning("Socket drain did not complete within {Timeout}; aborting remaining sockets.", _drainTimeout);
                _drainCts.Cancel();
                await drain;
            }
        }

        // Idempotent: ApplicationStopping and StopAsync both reach the drain, but it must run exactly once.
        private Task EnsureDraining()
        {
            lock (_drainLock)
            {
                return _drainTask ??= DrainAsync();
            }
        }

        private async Task DrainHandlerAsync(SocketHandler handler)
        {
            try
            {
                await handler.ShutdownAsync(_drainCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error draining socket {SocketId} on shutdown.", handler.Id);
            }
            finally
            {
                Unregister(handler.Id);
            }
        }

        /// <summary>
        /// Drains a socket that registered after the drain already snapshotted. Starts its loops so
        /// <see cref="SocketHandler.ShutdownAsync"/> has something to await, then closes it bounded by the
        /// same <see cref="_drainTimeout"/> — a latecomer whose client never completes the handshake trips
        /// the deadline (cancelling <see cref="_drainCts"/>) instead of blocking, exactly as the batch drain
        /// does. Faults are swallowed by <see cref="DrainHandlerAsync"/>, so the await can't throw.
        /// </summary>
        private async Task DrainLateSocketAsync(SocketHandler handler)
        {
            handler.Listen(_stoppingCts.Token, _drainCts.Token);

            var drain = DrainHandlerAsync(handler);
            try
            {
                await drain.WaitAsync(_drainTimeout);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("A socket registered during shutdown did not drain within {Timeout}; aborting it.", _drainTimeout);
                _drainCts.Cancel();
                await drain;
            }
        }

        public void Dispose()
        {
            _stoppingCts.Dispose();
            _drainCts.Dispose();
        }
    }
}
