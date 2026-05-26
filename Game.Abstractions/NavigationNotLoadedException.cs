namespace Game.Abstractions
{
    public class NavigationNotLoadedException : Exception
    {
        public NavigationNotLoadedException(string propertyName) : base($"Navigation data for '{propertyName}' not loaded.") { }
    }
}
