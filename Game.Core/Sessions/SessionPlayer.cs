using Game.Core.Entities;

namespace Game.Core.Sessions
{
    public class SessionPlayer
    {
        private readonly Player _player;

        public int Id => _player.Id;
        public string UserName => _player.UserName;
        public Guid Salt => _player.Salt;
        public string PassHash => _player.PassHash;
        public string Name => _player.Name;
        public int Level { get => _player.Level; set => _player.Level = value; }
        public int Exp { get => _player.Exp; set => _player.Exp = value; }
        public List<PlayerAttribute> Attributes { get => _player.PlayerAttributes; set => _player.PlayerAttributes = value; }
        public List<PlayerSkill> PlayerSkills => _player.PlayerSkills;
        public List<PlayerSkill> SelectedSkills => PlayerSkills.Where(skill => skill.Selected).ToList();
        public List<LogPreference> LogPreferences => _player.LogPreferences;
        public int StatPointsGained { get => _player.StatPointsGained; set => _player.StatPointsGained = value; }
        public int StatPointsUsed { get => _player.StatPointsUsed; private set => _player.StatPointsUsed = value; }

        public SessionPlayer(Player player)
        {
            _player = player;
        }

        public bool UpdateAttributes(IEnumerable<IAttributeUpdate> changedAttributes)
        {
            var availablePoints = StatPointsGained - StatPointsUsed;
            var matchedAttributes = Attributes.Where(att => att.IsCoreAttribute()).Select(att => (att, upd: changedAttributes.FirstOrDefault(chg => chg.AttributeId == att.AttributeId)));
            var changedPoints = matchedAttributes.Sum(match => match.upd?.Amount ?? 0);
            if (availablePoints - changedPoints >= 0 && matchedAttributes.All(match => match.att.Amount + (match.upd?.Amount ?? 0) >= 0))
            {
                StatPointsUsed += availablePoints - changedPoints;
                foreach (var (att, upd) in matchedAttributes)
                {
                    if (upd is not null)
                    {
                        att.Amount += upd.Amount;
                    }
                }

                return true;
            }

            return false;
        }
    }
}
