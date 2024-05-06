using GameCore.DataAccess;
using GameCore.Entities.LogPreferences;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockLogPreferences : ILogPreferences
    {
        public List<LogPreference> GetPreferences(int playerId)
        {
            throw new NotImplementedException();
        }

        public void SavePreferences(int playerId, IEnumerable<LogPreference> prefs)
        {
            throw new NotImplementedException();
        }
    }
}
