using DataAccess.Models.LogPreferences;
using System.Data.SqlClient;

namespace DataAccess.Repositories
{
    internal class LogPreferences : BaseRepository, ILogPreferences
    {
        public LogPreferences(string connectionString) : base(connectionString) { }

        public List<LogPreference> GetPreferences(int playerId)
        {
            var commandText = @"
                SELECT
                    LogSettingName AS Name,
                    Enabled
                FROM LogPreferences
                INNER JOIN LogSettings
                ON LogPreferences.LogSettingId = LogSettings.LogSettingId
                WHERE
                    PlayerId = @PlayerId";

            return QueryToList<LogPreference>(commandText, new SqlParameter("@PlayerId", playerId));
        }

        public void SavePreferences(int playerId, IEnumerable<LogPreference> prefs)
        {
            var groupedPrefs = prefs.GroupBy(p => p.Enabled);
            var enabledPrefs = groupedPrefs.FirstOrDefault(g => g.Key)?.Select(p => p.Name);
            var enabledStr = enabledPrefs is null ? "" : string.Join(',', enabledPrefs);
            var disabledPrefs = groupedPrefs.FirstOrDefault(g => !g.Key)?.Select(p => p.Name);
            var disabledStr = disabledPrefs is null ? "" : string.Join(',', disabledPrefs);

            var commandText = @"
                UPDATE LP
                    SET Enabled = 0
                FROM LogPreferences LP
                INNER JOIN LogSettings LS
                ON LP.LogSettingId = LS.LogSettingId
                INNER JOIN STRING_SPLIT(@DisabledSettings, ',') DS
                ON DS.value = LS.LogSettingName
                WHERE
                    PlayerId = @PlayerId

                UPDATE LP
                    SET Enabled = 1
                FROM LogPreferences LP
                INNER JOIN LogSettings LS
                ON LP.LogSettingId = LS.LogSettingId
                INNER JOIN STRING_SPLIT(@EnabledSettings, ',') ES
                ON ES.value = LS.LogSettingName
                WHERE
                    PlayerId = @PlayerId";

            ExecuteNonQuery(commandText,
                new SqlParameter("@PlayerId", playerId),
                new SqlParameter("@EnabledSettings", enabledStr),
                new SqlParameter("@DisabledSettings", disabledStr)
                );
        }
    }

    public interface ILogPreferences
    {
        public List<LogPreference> GetPreferences(int playerId);
        public void SavePreferences(int playerId, IEnumerable<LogPreference> prefs);
    }
}
