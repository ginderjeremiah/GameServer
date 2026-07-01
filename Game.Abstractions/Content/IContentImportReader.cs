namespace Game.Abstractions.Content
{
    /// <summary>
    /// Reads the source-controlled content export (<c>content/*.json</c>, spike #1390) into a
    /// <see cref="ContentImport"/>, deserializing through the same canonical JSON contract the exporter writes.
    /// </summary>
    public interface IContentImportReader
    {
        /// <summary>Reads every static reference set from the JSON files under <paramref name="directory"/>.</summary>
        ContentImport Read(string directory);

        /// <summary>Reads the export from the content directory shipped alongside the running application
        /// (the copy packaged into the build output), for the startup seeder.</summary>
        ContentImport ReadDefault();
    }
}
