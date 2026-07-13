using Game.Api.Models.Common;
using Game.Api.Models.Progress;
using Game.Application.Services;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Computes and applies the connected player's offline progress, returning the welcome-back summary. The
    /// frontend issues this once after the socket connects and reference data loads, but <strong>before</strong>
    /// starting the idle loop, so simulated and live battles never overlap (spike #879). The orchestration runs
    /// inline in <see cref="OfflineProgressService.SimulateOfflineProgress"/>; this command resolves the
    /// player/state from the session, persists the (battle-resolution) state change, and projects the summary.
    /// </summary>
    public class GetOfflineProgress : AbstractSocketCommandWithResponseData<OfflineProgressModel>
    {
        private readonly OfflineProgressService _offlineProgressService;

        public override string Name { get; set; } = nameof(GetOfflineProgress);

        public GetOfflineProgress(OfflineProgressService offlineProgressService)
        {
            _offlineProgressService = offlineProgressService;
        }

        public override async Task<ApiSocketResponse<OfflineProgressModel>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var player = context.Session.Player;
            var state = context.Session.PlayerState;

            var summary = await _offlineProgressService.SimulateOfflineProgress(player, state, cancellationToken);

            // Resolving a stale in-flight battle clears it from the session state, so persist the state.
            await context.Session.SavePlayerStateAsync(cancellationToken);

            return Success(OfflineProgressModel.FromSource(summary));
        }
    }
}
