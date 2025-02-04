using static Game.Core.ELogType;

namespace Game.Core.Logging
{
    /// <inheritdoc cref="ELogType"/>
    public class LogType
    {
        /// <summary>
        /// The enum value of the log type.
        /// </summary>
        public ELogType Value { get; set; }

        /// <summary>
        /// The default value for whether the log type should be visible.
        /// </summary>
        public bool DefaultValue { get; set; }

        /// <summary>
        /// Creates a new log type based on the given enum value.
        /// </summary>
        /// <param name="value"></param>
        public LogType(ELogType value)
        {
            Value = value;
            DefaultValue = GetDefaultValue(value);
        }

        private static bool GetDefaultValue(ELogType value)
        {
            return value switch
            {
                Damage => false,
                Debug => false,
                Exp => true,
                LevelUp => true,
                ItemFound => true,
                EnemyDefeated => true,
                _ => false,
            };
        }
    }
}
