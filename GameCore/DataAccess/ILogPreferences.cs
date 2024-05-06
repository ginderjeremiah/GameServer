using GameCore.Entities.LogPreferences;

namespace GameCore.DataAccess
{
    public interface ILogPreferences
    {
        public List<LogPreference> GetPreferences(int playerId);
        public void SavePreferences(int playerId, IEnumerable<LogPreference> prefs);
    }
}
