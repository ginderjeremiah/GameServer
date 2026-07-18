namespace Game.Abstractions.Content
{
    /// <summary>
    /// Reads the source-controlled content export (<c>content/*.json</c>, spike #1390) into a
    /// <see cref="ContentGraph"/>, deserializing through the same canonical JSON contract the exporter writes.
    /// Shared by the startup content seeder and the CI progression-graph lint — both read the exact same 12
    /// files into the exact same shape, so there is exactly one reader.
    /// </summary>
    public interface IContentImportReader
    {
        /// <summary>Reads every static reference set from the JSON files under <paramref name="directory"/>.</summary>
        ContentGraph Read(string directory);

        /// <summary>Reads the export from the content directory shipped alongside the running application
        /// (the copy packaged into the build output), for the startup seeder.</summary>
        ContentGraph ReadDefault();
    }
}
