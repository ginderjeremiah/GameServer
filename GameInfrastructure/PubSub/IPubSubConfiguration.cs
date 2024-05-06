namespace GameInfrastructure.PubSub
{
    public interface IPubSubConfiguration
    {
        public PubSubSystem PubSubSystem { get; }
        public string PubSubConnectionString { get; }
    }
}
