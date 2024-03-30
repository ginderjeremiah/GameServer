﻿using DataAccess.Models.PlayerAttributes;
using DataAccess.Models.Players;
using DataAccess.Models.SessionStore;
using GameServer.Models.Request;
using System.Text.Json.Serialization;

namespace GameServer.Auth
{
    public class SessionPlayer
    {
        private readonly SessionData _sessionData;

        private Player Player => _sessionData.PlayerData;

        public int PlayerId { get => Player.PlayerId; }
        public string UserName { get => Player.UserName; }
        [JsonIgnore]
        public Guid Salt { get => Player.Salt; }
        [JsonIgnore]
        public string PassHash { get => Player.PassHash; }
        public string PlayerName { get => Player.PlayerName; }
        public int Level { get => Player.Level; set => Player.Level = value; }
        public int Exp { get => Player.Exp; set => Player.Exp = value; }
        public List<PlayerAttribute> Attributes { get => _sessionData.Attributes; set => _sessionData.Attributes = value; }
        public List<int> SelectedSkills { get => _sessionData.SelectedSkills; set => _sessionData.SelectedSkills = value; }
        public int StatPointsGained { get => Player.StatPointsGained; set => Player.StatPointsGained = value; }
        public int StatPointsUsed { get => Player.StatPointsUsed; private set => Player.StatPointsUsed = value; }

        public SessionPlayer(SessionData sessionData)
        {
            _sessionData = sessionData;
        }

        public bool UpdateAttributes(List<AttributeUpdate> changedAttributes)
        {
            var availablePoints = StatPointsGained - StatPointsUsed;
            var changedPoints = changedAttributes.Sum(att => att.Amount);
            var matchedAtts = Attributes.Select(att => (att, upd: changedAttributes.FirstOrDefault(chg => chg.AttributeId == att.AttributeId)));
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
