using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Game.Api.Models.Progress;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Returns the connected player's challenge progress (player progress). Unlike the reference-data
    /// commands this is player-scoped: it resolves the player from the socket session and delegates to
    /// <see cref="IPlayerProgressRepository.GetChallenges"/> — the same read the <c>GET /api/Challenges/Player</c>
    /// endpoint runs (cache-first, so it serves warm cached progress). Named to stay unambiguous against
    /// the reference-data <see cref="GetChallenges"/> command, which returns the challenge definitions
    /// rather than the player's progress.
    /// </summary>
    public class GetPlayerChallenges : AbstractSocketCommandWithResponseData<IEnumerable<PlayerChallenge>>
    {
        private readonly IPlayerProgressRepository _playerProgress;

        public override string Name { get; set; } = nameof(GetPlayerChallenges);

        public GetPlayerChallenges(IPlayerProgressRepository playerProgress)
        {
            _playerProgress = playerProgress;
        }

        public override async Task<ApiSocketResponse<IEnumerable<PlayerChallenge>>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var progress = await _playerProgress.GetChallenges(context.Session.SelectedPlayerId);
            return Success(progress.To().Model<PlayerChallenge>());
        }
    }
}
