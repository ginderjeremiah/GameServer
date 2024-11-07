namespace Game.Core.CustomAttributes
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public class MetadataAttribute(string key, object value) : Attribute
    {
        public string Key { get; set; } = key;
        public object Value { get; set; } = value;
    }
}
