using Game.Api.Http;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Covers the branching header parsing in <see cref="ClientHints"/> — the empty→null normalization of
    /// the optional client-hint headers and the device-fingerprint header read — which previously had no
    /// direct coverage.
    /// </summary>
    public class ClientHintsTests
    {
        [Fact]
        public void FromHeaders_ReadsUserAgentAndClientHints()
        {
            var headers = new HeaderDictionary
            {
                ["User-Agent"] = "Mozilla/5.0",
                ["Sec-CH-UA"] = "\"Chromium\";v=\"120\"",
                ["Sec-CH-UA-Mobile"] = "?0",
                ["Sec-CH-UA-Platform"] = "\"Windows\"",
            };

            var hints = ClientHints.FromHeaders(headers);

            Assert.Equal("Mozilla/5.0", hints.UserAgent);
            Assert.Equal("\"Chromium\";v=\"120\"", hints.SecChUa);
            Assert.Equal("?0", hints.SecChUaMobile);
            Assert.Equal("\"Windows\"", hints.SecChUaPlatform);
        }

        [Fact]
        public void FromHeaders_MissingClientHints_AreNull()
        {
            var hints = ClientHints.FromHeaders(new HeaderDictionary { ["User-Agent"] = "UA" });

            Assert.Equal("UA", hints.UserAgent);
            Assert.Null(hints.SecChUa);
            Assert.Null(hints.SecChUaMobile);
            Assert.Null(hints.SecChUaPlatform);
        }

        [Fact]
        public void FromHeaders_EmptyClientHintValues_NormalizeToNull()
        {
            var headers = new HeaderDictionary
            {
                ["Sec-CH-UA"] = "",
                ["Sec-CH-UA-Mobile"] = "",
                ["Sec-CH-UA-Platform"] = "",
            };

            var hints = ClientHints.FromHeaders(headers);

            // The user-agent is reported verbatim (an absent header yields an empty string), while the
            // optional hints are normalized to null.
            Assert.Equal(string.Empty, hints.UserAgent);
            Assert.Null(hints.SecChUa);
            Assert.Null(hints.SecChUaMobile);
            Assert.Null(hints.SecChUaPlatform);
        }

        // A well-formed fingerprint: 64 lowercase hex characters, matching the frontend's SHA-256 digest.
        private const string ValidFingerprint = "a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1";

        [Fact]
        public void DeviceFingerprint_ReturnsHeaderValue_WhenPresentAndWellFormed()
        {
            var headers = new HeaderDictionary
            {
                [ClientHints.DeviceFingerprintHeader] = ValidFingerprint,
            };

            Assert.Equal(ValidFingerprint, ClientHints.DeviceFingerprint(headers));
        }

        [Fact]
        public void DeviceFingerprint_IsNull_WhenHeaderMissingOrEmpty()
        {
            Assert.Null(ClientHints.DeviceFingerprint(new HeaderDictionary()));
            Assert.Null(ClientHints.DeviceFingerprint(new HeaderDictionary
            {
                [ClientHints.DeviceFingerprintHeader] = "",
            }));
        }

        [Theory]
        [InlineData("fp-abc123")] // arbitrary client-supplied text, not a hex digest
        [InlineData("A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1")] // uppercase hex
        [InlineData("a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1")] // 63 chars, one short
        [InlineData("a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1aa")] // 66 chars, too long
        [InlineData("a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1\n")] // 64 valid chars + trailing \n — $ (not \z) would let this through
        public void DeviceFingerprint_IsNull_WhenHeaderIsNotAWellFormedHexDigest(string malformed)
        {
            var headers = new HeaderDictionary
            {
                [ClientHints.DeviceFingerprintHeader] = malformed,
            };

            Assert.Null(ClientHints.DeviceFingerprint(headers));
        }
    }
}
