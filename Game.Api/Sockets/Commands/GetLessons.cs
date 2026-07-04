using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Returns the full tutorial-lesson reference-data collection — the authored lessons the client's trigger
    /// evaluation and Help screen render from (spike #1392).
    /// </summary>
    public class GetLessons : AbstractReferenceDataCommand<Lesson>
    {
        private readonly ILessons _lessons;

        public override string Name { get; set; } = nameof(GetLessons);

        public GetLessons(ILessons lessons)
        {
            _lessons = lessons;
        }

        protected override IEnumerable<Lesson> GetReferenceData()
        {
            return _lessons.AllLessons();
        }

        protected override object VersionKey => _lessons.VersionKey;
    }
}
