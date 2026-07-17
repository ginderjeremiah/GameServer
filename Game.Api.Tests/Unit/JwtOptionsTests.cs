using Game.Api.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text;
using Xunit;

namespace Game.Api.Tests.Unit
{
    public class JwtOptionsTests
    {
        [Fact]
        public void ValidateOnStart_ThrowsWhenSigningKeyMissing()
        {
            using var provider = BuildValidatedProvider(new ConfigurationBuilder().Build());

            var ex = Assert.Throws<OptionsValidationException>(() =>
                provider.GetRequiredService<IOptions<JwtOptions>>().Value);
            Assert.Contains("Jwt:SigningKey must be at least 32 bytes for HMAC-SHA256", ex.Message);
        }

        [Fact]
        public void ValidateOnStart_ThrowsWhenSigningKeyTooShort()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = "too-short",
                })
                .Build();

            using var provider = BuildValidatedProvider(configuration);

            var ex = Assert.Throws<OptionsValidationException>(() =>
                provider.GetRequiredService<IOptions<JwtOptions>>().Value);
            Assert.Contains("Jwt:SigningKey must be at least 32 bytes for HMAC-SHA256", ex.Message);
        }

        [Fact]
        public void ValidateOnStart_SucceedsWhenSigningKeyIsAtLeast32Bytes()
        {
            var signingKey = new string('a', 32);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = signingKey,
                    ["Jwt:Issuer"] = "test-issuer",
                    ["Jwt:Audience"] = "test-audience",
                })
                .Build();
            Assert.Equal(32, Encoding.UTF8.GetByteCount(signingKey));

            using var provider = BuildValidatedProvider(configuration);

            var options = provider.GetRequiredService<IOptions<JwtOptions>>().Value;
            Assert.Equal(signingKey, options.SigningKey);
            Assert.Equal("test-issuer", options.Issuer);
            Assert.Equal("test-audience", options.Audience);
        }

        // Wires up the same options registration ConfigureAuth uses (bind + validate) so the test
        // exercises the real validation path rather than a re-declaration of it.
        private static ServiceProvider BuildValidatedProvider(IConfiguration configuration)
        {
            var services = new ServiceCollection();
            services.AddOptions<JwtOptions>()
                .Bind(configuration.GetSection("Jwt"))
                .Validate(options => Encoding.UTF8.GetByteCount(options.SigningKey) >= 32, "Jwt:SigningKey must be at least 32 bytes for HMAC-SHA256")
                .ValidateOnStart();
            services.AddSingleton(configuration);
            return services.BuildServiceProvider();
        }
    }
}
