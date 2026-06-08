namespace Game.Api.CodeGen
{
    /// <summary>
    /// Resolves the filesystem paths used by the API code generator. Centralized so the dev-time
    /// startup hook (<see cref="Startup"/>) and the standalone <see cref="CodeGenCommand"/> compute
    /// the frontend target directory the same way.
    /// </summary>
    public static class CodeGenPaths
    {
        // Path segments, relative to the repository root, of the frontend directory the generated
        // TypeScript API client is written into.
        private static readonly string[] TypesDirectorySegments =
            ["UI", "src", "lib", "api", "types"];

        /// <summary>
        /// Builds the absolute path of the generated-types directory under the given repository root.
        /// </summary>
        public static string ResolveTargetDirectory(string repositoryRoot)
        {
            return Path.Combine([repositoryRoot, .. TypesDirectorySegments]);
        }

        /// <summary>
        /// Walks up from <paramref name="startDirectory"/> (defaulting to the running assembly's base
        /// directory) until it finds the repository root, identified by the presence of the
        /// <c>Game.sln</c> solution file.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when no <c>Game.sln</c> is found.</exception>
        public static string FindRepositoryRoot(string? startDirectory = null)
        {
            var origin = startDirectory ?? AppContext.BaseDirectory;
            var directory = new DirectoryInfo(origin);
            while (directory is not null)
            {
                if (directory.EnumerateFiles("Game.sln").Any())
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                $"Could not locate the repository root (no 'Game.sln' found walking up from '{origin}').");
        }
    }
}
