using Game.Abstractions.DataAccess;
using Game.Api.Models.Common;
using Game.Application.Services;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Marks a lesson's coach-mark tour as completed (spike #1392). A screen-anchored lesson plays immediately
    /// on first visit with no prior <see cref="UnlockLesson"/> call, so this also accepts a still-locked lesson
    /// and normalizes it straight to read. Idempotent: re-marking an already-read lesson (a Help-screen replay)
    /// is a no-op, not an error.
    /// </summary>
    public class MarkLessonRead : AbstractSocketCommandWithParams<int>
    {
        private readonly PlayerService _playerService;
        private readonly ILessons _lessons;

        public override string Name { get; set; } = nameof(MarkLessonRead);

        public MarkLessonRead(PlayerService playerService, ILessons lessons)
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
            await _playerService.MarkLessonRead(player, Parameters, cancellationToken);

            return Success();
        }
    }
}
