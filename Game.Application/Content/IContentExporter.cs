namespace Game.Application.Content
{
    /// <summary>
    /// Serializes the static reference-data graph to the canonical, source-controlled JSON snapshot (spike
    /// #1390). Implements authoring model (A) — DB-primary, one-way export: the Workbench stays canonical and
    /// this mirrors the published read contracts to <c>content/*.json</c> used only as a seed/snapshot. The
    /// JSON is generated, never hand-edited; a CI drift guard re-derives it and asserts byte-equality.
    /// </summary>
    public interface IContentExporter
    {
        /// <summary>
        /// Exports every static reference set, in a stable file order, as canonical JSON. Reads the in-memory
        /// reference caches (the same published projection the client and Workbench receive), so callers must
        /// ensure the caches reflect the intended database state first.
        /// </summary>
        IReadOnlyList<ContentExportFile> ExportAll();
    }
}
