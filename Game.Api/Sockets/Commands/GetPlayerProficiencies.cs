using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Game.Api.Models.Progress;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Returns the connected player's proficiency progress (level + XP per proficiency). Player-scoped like
    /// <see cref="GetPlayerChallenges"/>: it resolves the player from the socket session and delegates to
    /// <see cref="IPlayerProgressRepository.GetProficiencies"/> (cache-first, so it serves warm cached
    /// progress). Named to stay unambiguous against the reference-data proficiency command that returns the
    /// authored definitions rather than the player's progress.
    /// </summary>
    public class GetPlayerProficiencies : AbstractSocketCommandWithResponseData<IEnumerable<PlayerProficiency>>
    {
        private readonly IPlayerProgressRepository _playerProgress;

        public override string Name { get; set; } = nameof(GetPlayerProficiencies);

        public GetPlayerProficiencies(IPlayerProgressRepository playerProgress)
        {
            _playerProgress = playerProgress;
        }

        public override async Task<ApiSocketResponse<IEnumerable<PlayerProficiency>>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var proficiencies = await _playerProgress.GetProficiencies(context.Session.SelectedPlayerId, cancellationToken);
            return Success(proficiencies.To().Model<PlayerProficiency>());
        }
    }
}
