using DataAccess.Entities.PlayerAttributes;
using DataAccess.Entities.Players;
using DataAccess.Entities.SessionStore;
using GameServer.Models.Attributes;

namespace GameServer.Auth
{
    public class SessionPlayer
    {
        private readonly SessionData _sessionData;

        private Player Player => _sessionData.PlayerData;

        public int PlayerId { get => Player.PlayerId; }
        public string UserName { get => Player.UserName; }
        public Guid Salt { get => Player.Salt; }
        public string PassHash { get => Player.PassHash; }
        public string PlayerName { get => Player.PlayerName; }
        public int Level { get => Player.Level; set => Player.Level = value; }
        public int Exp { get => Player.Exp; set => Player.Exp = value; }
        public List<PlayerAttribute> Attributes { get => _sessionData.Attributes; set => _sessionData.Attributes = value; }
        public List<PlayerSkill> PlayerSkills { get => _sessionData.PlayerSkills; }
        public List<int> SelectedSkills { get => _sessionData.PlayerSkills.Where(skill => skill.Selected).Select(skill => skill.SkillId).ToList(); }
        public int StatPointsGained { get => Player.StatPointsGained; set => Player.StatPointsGained = value; }
        public int StatPointsUsed { get => Player.StatPointsUsed; private set => Player.StatPointsUsed = value; }

        public SessionPlayer(SessionData sessionData)
        {
            _sessionData = sessionData;
        }

        public bool UpdateAttributes(List<AttributeUpdate> changedAttributes)
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
