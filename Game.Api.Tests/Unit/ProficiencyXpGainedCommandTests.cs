using Game.Api.Models.Progress;
using Game.Api.Sockets.Commands;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Unit coverage for the server-initiated <see cref="ProficiencyXpGained"/> push command: it echoes the
    /// emitted payload straight back as its response data (the client listens for it), and classifies a missing
    /// payload as <see cref="MalformedSocketCommandParametersException"/> like every other command's
    /// <c>SetParameters</c>. The handler ignores the socket context, so these run as plain unit tests without a
    /// live socket — mirroring the other echo push commands.
    /// </summary>
    public class ProficiencyXpGainedCommandTests
    {
        private static ProficiencyXpGained NewCommand()
        {
            return new ProficiencyXpGained { Parameters = new ProficiencyXpGainedModel { Proficiencies = [], Opened = [] } };
        }

        [Fact]
        public async Task SetParameters_ThenExecute_EchoesThePayloadBack()
        {
            var model = new ProficiencyXpGainedModel
            {
                Proficiencies =
                [
                    new ProficiencyXpResultModel
                    {
                        ProficiencyId = 3,
                        XpGained = 12.5m,
                        NewLevel = 2,
                        NewXp = 4m,
                        MilestonesCrossed = [5],
                        GrantedSkillIds = [9],
                    },
                ],
                Opened = [new ProficiencyOpenedModel { ProficiencyId = 4 }],
            };

            var command = NewCommand();
            // The emitted info carries the serialized payload exactly as the notifier would send it.
            command.SetParameters(new ProficiencyXpGainedInfo(model).Parameters);

            var response = await command.HandleExecuteAsync(context: null!, CancellationToken.None);

            Assert.Equal(nameof(ProficiencyXpGained), response.Name);
            var echoed = Assert.Single(response.Data.Proficiencies);
            Assert.Equal(3, echoed.ProficiencyId);
            Assert.Equal(12.5m, echoed.XpGained);
            Assert.Equal(2, echoed.NewLevel);
            Assert.Equal(4m, echoed.NewXp);
            Assert.Equal([5], echoed.MilestonesCrossed);
            Assert.Equal([9], echoed.GrantedSkillIds);
            var opened = Assert.Single(response.Data.Opened);
            Assert.Equal(4, opened.ProficiencyId);
        }

        [Fact]
        public void SetParameters_NullPayload_ThrowsMalformedSocketCommandParametersException()
        {
            var command = NewCommand();

            var ex = Assert.Throws<MalformedSocketCommandParametersException>(() => command.SetParameters(null));

            Assert.IsType<ArgumentNullException>(ex.InnerException);
        }
    }
}
