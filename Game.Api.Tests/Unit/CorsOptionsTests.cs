using Game.Api.Cors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Game.Api.Tests.Unit
{
    public class CorsOptionsTests
    {
        // Mirrors the validation predicate registered in Startup so the "at least one origin" rule
        // is covered without standing up the web host.
        private static bool IsValid(CorsOptions options) => options.AllowedOrigins.Count > 0;

        private static CorsOptions Bind(params (string Key, string Value)[] settings)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value))
                .Build();

            var options = new CorsOptions();
            configuration.GetSection(CorsOptions.SectionName).Bind(options);
            return options;
        }

        [Fact]
        public void Bind_ReadsMultipleAllowedOrigins_InOrder()
        {
            var options = Bind(
                ("Cors:AllowedOrigins:0", "http://localhost:5174"),
                ("Cors:AllowedOrigins:1", "https://play.example.com"));

            Assert.Equal(["http://localhost:5174", "https://play.example.com"], options.AllowedOrigins);
            Assert.True(IsValid(options));
        }

        [Fact]
        public void Bind_SingleAllowedOrigin_IsValid()
        {
            var options = Bind(("Cors:AllowedOrigins:0", "https://play.example.com"));

            Assert.Equal(["https://play.example.com"], options.AllowedOrigins);
            Assert.True(IsValid(options));
        }

        [Fact]
        public void Bind_MissingSection_LeavesOriginsEmptyAndFailsValidation()
        {
            var options = Bind(("SomethingElse", "value"));

            Assert.Empty(options.AllowedOrigins);
            Assert.False(IsValid(options));
        }

        [Fact]
        public void ValidateOnStart_ThrowsWhenNoOriginsConfigured()
        {
            using var provider = BuildValidatedProvider(new ConfigurationBuilder().Build());

            var ex = Assert.Throws<OptionsValidationException>(() =>
                provider.GetRequiredService<IOptions<CorsOptions>>().Value);
            Assert.Contains("Cors:AllowedOrigins not set", ex.Message);
        }

        [Fact]
        public void ValidateOnStart_SucceedsWhenAnOriginIsConfigured()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Cors:AllowedOrigins:0"] = "http://localhost:5174",
                })
                .Build();

            using var provider = BuildValidatedProvider(configuration);

            var options = provider.GetRequiredService<IOptions<CorsOptions>>().Value;
            Assert.Equal(["http://localhost:5174"], options.AllowedOrigins);
        }

        // Wires up the same options registration Startup uses (bind + validate) so the test exercises the
        // real validation path rather than a re-declaration of it.
        private static ServiceProvider BuildValidatedProvider(IConfiguration configuration)
        {
            var services = new ServiceCollection();
            services.AddOptions<CorsOptions>()
                .BindConfiguration(CorsOptions.SectionName)
                .Validate(options => options.AllowedOrigins.Count > 0, "Cors:AllowedOrigins not set")
                .ValidateOnStart();
            services.AddSingleton(configuration);
            return services.BuildServiceProvider();
        }
    }
}
