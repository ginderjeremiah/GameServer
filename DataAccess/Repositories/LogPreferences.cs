using GameCore.DataAccess;
using GameCore.Entities.LogPreferences;
using GameCore.Infrastructure;

namespace DataAccess.Repositories
{
    internal class LogPreferences : BaseRepository, ILogPreferences
    {
        public LogPreferences(IDatabaseService database) : base(database) { }

        public List<LogPreference> GetPreferences(int playerId)
        {
            var commandText = @"
                SELECT
                    LP.PlayerId,
                    LS.LogSettingName AS Name,
                    COALESCE(LP.Enabled, LS.DefaultValue) AS Enabled
                FROM LogSettings LS
                LEFT JOIN LogPreferences LP 
                ON LP.LogSettingId = LS.LogSettingId
                AND LP.PlayerId = @PlayerId";

            return Database.QueryToList<LogPreference>(commandText, new QueryParameter("@PlayerId", playerId));
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

                INSERT INTO LogPreferences
                (PlayerId, LogSettingId, Enabled)
                SELECT
                    @PlayerId,
                    LS.LogSettingId,
                    0
                FROM STRING_SPLIT(@DisabledSettings, ',') DS
                INNER JOIN LogSettings LS
                    ON DS.value = LS.LogSettingName
                LEFT JOIN LogPreferences LP
                    ON LS.LogSettingId = LP.LogSettingId
                    AND LP.PlayerId = @PlayerId
                WHERE
                    LP.Enabled IS NULL

                UPDATE LP
                    SET Enabled = 1
                FROM LogPreferences LP
                INNER JOIN LogSettings LS
                ON LP.LogSettingId = LS.LogSettingId
                INNER JOIN STRING_SPLIT(@EnabledSettings, ',') ES
                ON ES.value = LS.LogSettingName
                WHERE
                    PlayerId = @PlayerId

                INSERT INTO LogPreferences
                (PlayerId, LogSettingId, Enabled)
                SELECT
                    @PlayerId,
                    LS.LogSettingId,
                    1
                FROM STRING_SPLIT(@EnabledSettings, ',') ES
                INNER JOIN LogSettings LS
                    ON ES.value = LS.LogSettingName
                LEFT JOIN LogPreferences LP
                    ON LS.LogSettingId = LP.LogSettingId
                    AND LP.PlayerId = @PlayerId
                WHERE
                    LP.Enabled IS NULL";

            Database.ExecuteNonQuery(commandText,
                new QueryParameter("@PlayerId", playerId),
                new QueryParameter("@EnabledSettings", enabledStr),
                new QueryParameter("@DisabledSettings", disabledStr)
            );
        }
    }
}
