using Game.Api.Models.Common;
using Game.Application.Services;
using CoreLogPreference = Game.Core.Players.LogPreference;
using LogPreferenceModel = Game.Api.Models.Player.LogPreference;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Persists the player's combat-log display preferences. Mirrors
    /// <see cref="SetItemFavorite"/>: the change is applied to the cached domain
    /// player (the source of truth for player data) and persisted from there.
    /// </summary>
    public class SaveLogPreferences : AbstractSocketCommandWithParams<List<LogPreferenceModel>>
    {
        private readonly PlayerService _playerService;

        public override string Name { get; set; } = nameof(SaveLogPreferences);

        public SaveLogPreferences(PlayerService playerService)
        {
            _playerService = playerService;
        }

        public override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context)
        {
            if (Parameters.Any(p => !Enum.IsDefined(p.Id)))
            {
                return Error("Unknown log type.");
            }

            var player = await context.Session.LoadPlayer();
            await _playerService.SaveLogPreferences(
                player,
                Parameters.Select(p => new CoreLogPreference { LogType = p.Id, Enabled = p.Enabled }));

            return Success();
        }
    }
}
