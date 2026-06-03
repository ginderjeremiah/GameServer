using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.TestInfrastructure.Fixtures
{
    /// <summary>
    /// Connection details for backing-service containers that were started outside the test
    /// process — e.g. by the Claude Code <c>session-start</c> hook in a constrained sandbox where
    /// Testcontainers' bridge networking is unavailable (see
    /// https://github.com/anthropics/claude-code/issues/29515). When the marker file is present,
    /// the container fixtures reuse these services instead of provisioning their own via
    /// Testcontainers.
    /// </summary>
    public sealed record PreexistingContainerInfo
    {
        public const string MarkerFileName = ".container-info.json";

        /// <summary>PostgreSQL connection string (Npgsql format).</summary>
        [JsonPropertyName("postgres")]
        public required string Postgres { get; init; }

        /// <summary>Redis connection string (StackExchange.Redis format, e.g. <c>localhost:6379</c>).</summary>
        [JsonPropertyName("redis")]
        public required string Redis { get; init; }

        /// <summary>
        /// Loads the marker file by walking up from the test assembly's base directory, returning
        /// <c>null</c> when no marker exists (the normal case, where Testcontainers is used).
        /// </summary>
        public static PreexistingContainerInfo? TryLoad() => TryLoad(AppContext.BaseDirectory);

        /// <summary>
        /// Walks up from <paramref name="startDirectory"/> looking for the marker file. Exposed for
        /// testing; production code should call the parameterless <see cref="TryLoad()"/>.
        /// </summary>
        internal static PreexistingContainerInfo? TryLoad(string startDirectory)
        {
            for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
            {
                var markerPath = Path.Combine(directory.FullName, MarkerFileName);
                if (!File.Exists(markerPath))
                {
                    continue;
                }

                var json = File.ReadAllText(markerPath);
                return JsonSerializer.Deserialize<PreexistingContainerInfo>(json)
                    ?? throw new InvalidOperationException($"Container marker file at '{markerPath}' was empty or invalid.");
            }

            return null;
        }
    }
}
