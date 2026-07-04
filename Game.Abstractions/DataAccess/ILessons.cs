using Contracts = Game.Abstractions.Contracts;

namespace Game.Abstractions.DataAccess
{
    /// <summary>The tutorial-lesson reference set (spike #1392): pure content data with no battle/domain model,
    /// so the read surface is just the contract projection.</summary>
    public interface ILessons
    {
        /// <summary>Every lesson — the reference set the loading screen and the Help screen render from.</summary>
        List<Contracts.Lesson> AllLessons();

        /// <inheritdoc cref="IItems.VersionKey"/>
        object VersionKey { get; }
    }
}
