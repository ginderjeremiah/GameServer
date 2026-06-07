using Game.Api.Models;

namespace Game.Api.Models.ReferenceData
{
    /// <summary>
    /// The current content version of a single reference-data set, returned by
    /// <c>GetReferenceDataVersions</c>. The frontend compares <see cref="Version"/> against the
    /// version stored alongside its cached copy to decide whether to re-fetch the set.
    /// </summary>
    public class ReferenceDataVersion : IModel
    {
        /// <summary>The socket command that fetches this set (e.g. <c>GetZones</c>).</summary>
        public string Command { get; set; } = "";

        /// <summary>A content hash that changes whenever the set's client-visible data changes.</summary>
        public string Version { get; set; } = "";
    }
}
