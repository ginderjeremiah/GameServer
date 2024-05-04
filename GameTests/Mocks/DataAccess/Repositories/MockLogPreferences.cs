using DataAccess.Entities.LogPreferences;
using DataAccess.Repositories;

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
