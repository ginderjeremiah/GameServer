namespace Game.DataAccess
{
    internal class DomainEventEnvelope
    {
        public required string Type { get; set; }
        public required string Payload { get; set; }
    }
}
