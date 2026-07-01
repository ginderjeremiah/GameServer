using Game.Application.Content;
using Game.Core;
using Game.Core.Progress;
using Xunit;
using CoreChallenge = Game.Core.Progress.Challenge;

namespace Game.Application.Tests.Content
{
    /// <summary>
    /// Coverage for <see cref="ChallengeContractMapper"/> — the one reference set whose read contract is
    /// projected from the gameplay domain model (via <c>IChallenges.All</c>), not a DataAccess entity mapper.
    /// Guards that the authoring-only <see cref="CoreChallenge.DesignerNotes"/> survives that Core→contract
    /// projection, matching the round-trip assertions the entity-mapped sets carry.
    /// </summary>
    public class ChallengeContractMapperTests
    {
        [Fact]
        public void ToContract_RoundTripsDesignerNotesAndScalarFields()
        {
            var challenge = new CoreChallenge
            {
                Id = 3,
                Name = "Slayer",
                Description = "Kill enemies",
                DesignerNotes = "why this challenge exists",
                Type = new ChallengeType(EChallengeType.EnemiesKilled),
                ProgressGoal = 100m,
            };

            var contract = ChallengeContractMapper.ToContract(challenge);

            Assert.Equal(3, contract.Id);
            Assert.Equal("Slayer", contract.Name);
            Assert.Equal(EChallengeType.EnemiesKilled, contract.ChallengeTypeId);
            Assert.Equal("why this challenge exists", contract.DesignerNotes);
        }
    }
}
