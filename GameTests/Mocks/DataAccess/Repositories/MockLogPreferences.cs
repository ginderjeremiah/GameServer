using GameCore.DataAccess;
using GameCore.Entities.LogPreferences;

namespace GameTests.Mocks.DataAccess.Repositories
{
    internal class MockLogPreferences : ILogPreferences
    {
        public List<LogPreference> LogPreferences { get; set; } = new();
        public List<LogPreference> GetPreferences(int playerId)
        {
            return LogPreferences.Where(pref => pref.PlayerId == playerId).ToList();
        }

        public void SavePreferences(int playerId, IEnumerable<LogPreference> prefs)
        {
            foreach (var pref in prefs)
            {
                var foundPref = LogPreferences.FirstOrDefault(p => p.PlayerId == playerId && p.Name == pref.Name);
                if (foundPref is null)
                {
                    pref.PlayerId = playerId;
                    LogPreferences.Add(pref);
                }
                else
                {
                    foundPref.Enabled = pref.Enabled;
                }
            }
        }
    }
}
