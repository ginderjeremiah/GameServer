using Game.Api.Models.Common;
using Game.Application.Services;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Replaces the player's equipped skill loadout in one atomic operation — select, deselect, and
    /// reorder all flow through this single command. Like <see cref="SaveLogPreferences"/>, the change
    /// is applied to the cached domain player (the source of truth) and persisted to the database via
    /// the write-behind <see cref="Game.Core.Players.Events.SelectedSkillsChangedEvent"/>. The backend
    /// cap and unlock checks are anti-cheat: an invalid loadout is rejected with an error rather than
    /// trusted from the client.
    /// </summary>
    public class SetSelectedSkills : AbstractSocketCommandWithParams<List<int>>
    {
        private readonly PlayerService _playerService;

        public override string Name { get; set; } = nameof(SetSelectedSkills);

        public SetSelectedSkills(PlayerService playerService)
        {
            _playerService = playerService;
        }

        public override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var player = context.Session.Player;
            var success = await _playerService.SetSelectedSkills(player, Parameters);

            return success ? Success() : Error("Failed to set selected skills.");
        }
    }
}
