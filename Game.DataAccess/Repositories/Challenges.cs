using Game.Abstractions.DataAccess;
using Game.Core;
using Game.Core.Progress;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess.Repositories
{
    internal class Challenges(GameContext context) : IChallenges
    {
        private static List<Challenge>? _challengeList;
        private readonly GameContext _context = context;

        public List<Challenge> All()
        {
            _challengeList ??= [.. _context.Challenges
                .AsNoTracking()
                .OrderBy(c => c.Id)
                .Select(c => new Challenge
                {
                    Id = c.Id,
                    Name = c.Name,
                    Description = c.Description,
                    Type = new ChallengeType((EChallengeType)c.ChallengeTypeId),
                    TargetEntityId = c.TargetEntityId,
                    ProgressGoal = c.ProgressGoal,
                    RewardItemId = c.RewardItemId,
                    RewardItemModId = c.RewardItemModId,
                })];

            return _challengeList;
        }

        public Challenge GetChallenge(int challengeId)
        {
            var challenges = All();
            return challenges[challengeId];
        }
    }
}
