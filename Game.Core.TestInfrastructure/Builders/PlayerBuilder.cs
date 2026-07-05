using Game.Core.Players;
using Game.Core.Players.Inventories;
using Game.Core.Skills;

namespace Game.Core.TestInfrastructure.Builders
{
    /// <summary>
    /// Fluent builder for the <see cref="Player"/> aggregate in tests. Supplies sensible defaults for
    /// every required member so a test specifies only the fields it cares about, and is the single place
    /// that knows the full <see cref="Player"/> graph — adding a required member to <see cref="Player"/>
    /// becomes a one-line change here instead of an edit across every test.
    /// </summary>
    public sealed class PlayerBuilder
    {
        private int _id = 1;
        private int _classId = 0;
        private string _name = "Test";
        private int _level = 1;
        private int _exp = 0;
        private int _currentZoneId = 0;
        private DateTime _lastActivity = DateTime.UtcNow;
        private bool _autoChallengeBoss = false;
        private List<StatAllocation> _statAllocations = [];
        private int _statPointsGained = 0;
        private int _statPointsUsed = 0;
        private Inventory _inventory = new();
        private List<Skill> _skills = [];
        private List<Skill> _selectedSkills = [];
        private List<LogPreference> _logPreferences = [];
        private List<PlayerLesson> _lessons = [];

        public PlayerBuilder WithId(int id)
        {
            _id = id;
            return this;
        }

        public PlayerBuilder WithClassId(int classId)
        {
            _classId = classId;
            return this;
        }

        public PlayerBuilder WithName(string name)
        {
            _name = name;
            return this;
        }

        public PlayerBuilder WithLevel(int level)
        {
            _level = level;
            return this;
        }

        public PlayerBuilder WithExp(int exp)
        {
            _exp = exp;
            return this;
        }

        public PlayerBuilder WithLastActivity(DateTime lastActivity)
        {
            _lastActivity = lastActivity;
            return this;
        }

        public PlayerBuilder WithAutoChallengeBoss(bool enabled)
        {
            _autoChallengeBoss = enabled;
            return this;
        }

        public PlayerBuilder WithStatAllocations(IEnumerable<StatAllocation> allocations)
        {
            _statAllocations = allocations.ToList();
            return this;
        }

        public PlayerBuilder WithStatPointsGained(int gained)
        {
            _statPointsGained = gained;
            return this;
        }

        public PlayerBuilder WithStatPointsUsed(int used)
        {
            _statPointsUsed = used;
            return this;
        }

        public PlayerBuilder WithInventory(Inventory inventory)
        {
            _inventory = inventory;
            return this;
        }

        public PlayerBuilder WithSkills(IEnumerable<Skill> skills)
        {
            _skills = skills.ToList();
            return this;
        }

        public PlayerBuilder WithSelectedSkills(IEnumerable<Skill> selectedSkills)
        {
            _selectedSkills = selectedSkills.ToList();
            return this;
        }

        public PlayerBuilder WithLessons(IEnumerable<PlayerLesson> lessons)
        {
            _lessons = lessons.ToList();
            return this;
        }

        public Player Build() => new()
        {
            Id = _id,
            ClassId = _classId,
            Name = _name,
            Level = _level,
            Exp = _exp,
            CurrentZoneId = _currentZoneId,
            LastActivity = _lastActivity,
            AutoChallengeBoss = _autoChallengeBoss,
            StatPoints = new PlayerStatPoints
            {
                StatAllocations = _statAllocations,
                StatPointsGained = _statPointsGained,
                StatPointsUsed = _statPointsUsed,
            },
            Inventory = _inventory,
            SelectedSkills = _selectedSkills,
            Skills = _skills,
            LogPreferences = _logPreferences,
            Lessons = _lessons,
        };
    }
}
