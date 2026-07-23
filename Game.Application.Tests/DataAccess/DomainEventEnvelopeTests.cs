using Game.Core;
using Game.DataAccess;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Unit tests for <see cref="DomainEventEnvelope"/>'s per-instance <c>Id</c>. It is pure in-process logic
    /// with no out-of-process dependency, so it is covered here classically rather than through an integration test.
    /// </summary>
    public class DomainEventEnvelopeTests
    {
        [Fact]
        public void TwoEnvelopesWithIdenticalTypeAndPayload_SerializeToDistinctRawStrings()
        {
            // Two independently-raised events can carry byte-identical Type+Payload (e.g. a duplicated
            // SkillUnlockedEvent, or two ItemFavoriteChangedEvents toggling to the same value). The write-behind
            // queue's stranded-head tracking and LREM acknowledge/dead-letter removal key off the raw serialized
            // string, so without a per-envelope identity, equal payloads would alias one another there (#2341).
            var first = new DomainEventEnvelope { Type = "SkillUnlockedEvent", Payload = "{\"playerId\":1,\"skillId\":2}" };
            var second = new DomainEventEnvelope { Type = "SkillUnlockedEvent", Payload = "{\"playerId\":1,\"skillId\":2}" };

            Assert.NotEqual(first.Id, second.Id);
            Assert.NotEqual(first.Serialize(), second.Serialize());
        }

        [Fact]
        public void Deserialize_MissingId_DefaultsRatherThanFailing()
        {
            // An envelope enqueued by a pre-upgrade instance mid-rolling-deploy carries no "id" field. Id is
            // therefore not required, so it must still deserialize cleanly on a newer instance rather than throw.
            var legacyMessage = "{\"type\":\"SkillUnlockedEvent\",\"payload\":\"{}\"}";

            var envelope = legacyMessage.Deserialize<DomainEventEnvelope>();

            Assert.NotNull(envelope);
            Assert.Equal("SkillUnlockedEvent", envelope.Type);
        }
    }
}
