namespace Game.Abstractions
{
    public class NotLoadedException : Exception
    {
        public NotLoadedException(string propertyName) : base($"Navigation data for '{propertyName}' not loaded.") { }
    }
}
