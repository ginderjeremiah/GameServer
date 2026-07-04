using Contracts = Game.Abstractions.Contracts;

namespace Game.Abstractions.DataAccess
{
    /// <summary>
    /// Read access to the cached tutorial-lesson catalogue (#1591, spike #1392): the contract projection the
    /// game client's Help screen (#1589) and the admin Workbench read. Lessons carry no battle/domain logic, so
    /// there is no lean domain model — only the read contract.
    /// </summary>
    public interface ILessons
    {
        public List<Contracts.Lesson> AllLessons();

        /// <inheritdoc cref="IItems.VersionKey"/>
        public object VersionKey { get; }
    }
}
