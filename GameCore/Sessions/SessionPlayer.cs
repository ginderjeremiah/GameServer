using GameCore.Entities;

namespace GameCore.Sessions
{
    public class SessionPlayer
    {
        private readonly SessionData _sessionData;

        private Player Player => _sessionData.PlayerData;

        public int PlayerId { get => Player.Id; }
        public string UserName { get => Player.UserName; }
        public Guid Salt { get => Player.Salt; }
        public string PassHash { get => Player.PassHash; }
        public string PlayerName { get => Player.Name; }
        public int Level { get => Player.Level; set => Player.Level = value; }
        public int Exp { get => Player.Exp; set => Player.Exp = value; }
        public List<PlayerAttribute> Attributes { get => _sessionData.Attributes; set => _sessionData.Attributes = value; }
        public List<PlayerSkill> PlayerSkills { get => _sessionData.Skills; }
        public List<PlayerSkill> SelectedSkills { get => _sessionData.Skills.Where(skill => skill.Selected).ToList(); }
        public int StatPointsGained { get => Player.StatPointsGained; set => Player.StatPointsGained = value; }
        public int StatPointsUsed { get => Player.StatPointsUsed; private set => Player.StatPointsUsed = value; }

        public SessionPlayer(SessionData sessionData)
        {
            _sessionData = sessionData;
        }

        public bool UpdateAttributes(List<IAttributeUpdate> changedAttributes)
        {
            var availablePoints = StatPointsGained - StatPointsUsed;
            var matchedAtts = Attributes.Where(att => att.IsCoreAttribute()).Select(att => (att, upd: changedAttributes.FirstOrDefault(chg => chg.AttributeId == att.AttributeId)));
            var changedPoints = matchedAtts.Sum(match => match.upd?.Amount ?? 0);
            if (availablePoints - changedPoints >= 0 && matchedAtts.All(match => match.att.Amount + (match.upd?.Amount ?? 0) >= 0))
            {
                StatPointsUsed += availablePoints - changedPoints;
                foreach (var (att, upd) in matchedAtts)
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
