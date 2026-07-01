namespace Game.TestInfrastructure.Helpers
{
    /// <summary>
    /// Locates source-controlled paths relative to the repository root by walking up from the test
    /// assembly's base directory (the same strategy <c>PreexistingContainerInfo</c> uses for the container
    /// marker). Tests run from a deep <c>bin/</c> directory, so they cannot assume a working directory.
    /// </summary>
    public static class RepoPaths
    {
        private const string RootMarker = "Game.sln";

        /// <summary>The repository root — the directory containing <c>Game.sln</c>.</summary>
        public static string RepoRoot()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, RootMarker)))
                {
                    return directory.FullName;
                }
            }

            throw new InvalidOperationException($"Could not locate the repository root (no '{RootMarker}' found above '{AppContext.BaseDirectory}').");
        }

        /// <summary>The <c>content/</c> directory holding the source-controlled reference-data export.</summary>
        public static string ContentDirectory() => Path.Combine(RepoRoot(), "content");
    }
}
