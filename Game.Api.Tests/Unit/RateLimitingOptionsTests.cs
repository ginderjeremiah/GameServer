using Game.Api.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Game.Api.Tests.Unit
{
    public class RateLimitingOptionsTests
    {
        [Fact]
        public void Defaults_AreSafePositiveLimits()
        {
            // Security hardening is on by default: an unconfigured deployment still gets a real limit.
            var options = new RateLimitingOptions();

            Assert.Equal(10, options.Auth.PermitLimit);
            Assert.Equal(60, options.Auth.WindowSeconds);
        }

        [Fact]
        public void Bind_OverridesOnlyTheConfiguredField()
        {
            var options = new RateLimitingOptions();
            BuildConfiguration(("RateLimiting:Auth:PermitLimit", "5"))
                .GetSection(RateLimitingOptions.SectionName)
                .Bind(options);

            Assert.Equal(5, options.Auth.PermitLimit);
            // The window keeps its default since config did not supply one.
            Assert.Equal(60, options.Auth.WindowSeconds);
        }

        [Fact]
        public void Validation_SucceedsWithDefaults_WhenSectionAbsent()
        {
            using var provider = BuildValidatedProvider(new ConfigurationBuilder().Build());

            var options = provider.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
            Assert.Equal(10, options.Auth.PermitLimit);
        }

        [Fact]
        public void Validation_ThrowsWhenPermitLimitNotPositive()
        {
            var configuration = BuildConfiguration(("RateLimiting:Auth:PermitLimit", "0"));

            using var provider = BuildValidatedProvider(configuration);

            var ex = Assert.Throws<OptionsValidationException>(() =>
                provider.GetRequiredService<IOptions<RateLimitingOptions>>().Value);
            Assert.Contains("RateLimiting:Auth", ex.Message);
        }

        private static IConfiguration BuildConfiguration(params (string Key, string Value)[] settings)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value))
                .Build();
        }

        // Exercises the real options registration (AddAuthRateLimiter) so the test pins the actual
        // bind + validate path rather than a re-declaration of it.
        private static ServiceProvider BuildValidatedProvider(IConfiguration configuration)
        {
            var services = new ServiceCollection();
            services.AddSingleton(configuration);
            services.AddAuthRateLimiter();
            return services.BuildServiceProvider();
        }
    }
}
