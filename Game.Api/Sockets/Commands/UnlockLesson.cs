using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Game.Application.Services;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Records that a mechanic-anchored lesson's trigger fired client-side (spike #1392). Client-detected
    /// triggers are trusted — nothing is rewarded, so a dishonest client can only show itself tutorials early.
    /// Idempotent: unlocking an already-unlocked (or already-read) lesson is a no-op, not an error.
    /// </summary>
    public class UnlockLesson : AbstractSocketCommandWithParams<int>
    {
        private readonly PlayerService _playerService;
        private readonly ILessons _lessons;

        public override string Name { get; set; } = nameof(UnlockLesson);

        public UnlockLesson(PlayerService playerService, ILessons lessons)
        {
            _playerService = playerService;
            _lessons = lessons;
        }

        public override async Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            if (!_lessons.ValidateLessonId(Parameters))
            {
                return Error("Unknown lesson.");
            }

            var player = context.Session.Player;
            await _playerService.UnlockLesson(player, Parameters, cancellationToken);

            return Success();
        }
    }
}
