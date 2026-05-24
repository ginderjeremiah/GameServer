using Game.Abstractions.DataAccess;
using Game.Abstractions.Entities;
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
                .OrderBy(c => c.Id)];

            return _challengeList;
        }

        public Challenge? GetChallenge(int challengeId)
        {
            var challenges = All();
            return challenges.Count <= challengeId ? null : challenges[challengeId];
        }
    }
}
