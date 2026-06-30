namespace Game.Application.Content
{
    /// <summary>One serialized static reference set in the content export: its on-disk file name and the
    /// canonical JSON body (trailing newline included). The <see cref="FileName"/> is the relative path under
    /// the repo's <c>content/</c> directory the seed (#1419) and lint (#1420) consume.</summary>
    public sealed record ContentExportFile(string FileName, string Json);
}
