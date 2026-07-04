using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Serves the full tutorial-lesson reference-data set over the socket (#1591, spike #1392). The client
    /// evaluates each lesson's trigger (#1587) and tracks its own per-player unread/read state (#1588).
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
