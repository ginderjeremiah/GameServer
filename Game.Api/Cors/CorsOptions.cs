namespace Game.Api.Cors
{
    /// <summary>
    /// Configuration for the browser CORS policy, bound from the "Cors" configuration section. The
    /// frontend origins allowed to call the API are deployment-specific and must be supplied via
    /// configuration; the local dev origin is provided in <c>appsettings.Development.json</c> so local
    /// behaviour is unchanged, while every other environment must set at least one origin.
    /// </summary>
    public class CorsOptions
    {
        /// <summary>The configuration section this options class binds from.</summary>
        public const string SectionName = "Cors";

        public List<string> AllowedOrigins { get; set; } = [];
    }
}
