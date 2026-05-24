namespace Game.Core.Attributes.Modifiers
{
    /// <summary>
    /// This comparer is intended to be used in a SortedLinkedList that is only used internally and is guaranteed to never contain null values.
    /// </summary>
    internal class AttributeModifierNullUnsafeTypeComparer : IComparer<AttributeModifier>
    {
        public int Compare(AttributeModifier? x, AttributeModifier? y)
        {
            return x!.Type - y!.Type;
        }
    }
}
