namespace Game.Abstractions.Contracts
{
    /// <summary>
    /// Implemented by every read contract carrying the authoring-only <c>DesignerNotes</c> field (see
    /// "Designer notes" in backend.md). Lets <c>AbstractReferenceDataCommand</c> redact the field for a
    /// non-admin connection generically, without a per-set override.
    /// </summary>
    public interface IHasDesignerNotes
    {
        string DesignerNotes { get; set; }
    }
}
