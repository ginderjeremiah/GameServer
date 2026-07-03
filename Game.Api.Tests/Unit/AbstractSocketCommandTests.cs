using Game.Api.Models.Common;
using Game.Api.Sockets;
using Game.Api.Sockets.Commands;
using System.Text.Json;
using Xunit;

namespace Game.Api.Tests.Unit
{
    /// <summary>
    /// Verifies that parameter binding classifies a malformed/missing <c>Parameters</c> payload as
    /// <see cref="MalformedSocketCommandParametersException"/> right where the semantics are known — inside
    /// <c>SetParameters</c> itself — rather than a caller further up (<see cref="SocketHandler"/>) guessing
    /// from the exception's type, which could otherwise misclassify an unrelated <see cref="JsonException"/>
    /// or <see cref="ArgumentNullException"/> thrown elsewhere (e.g. command construction) (#1498).
    /// </summary>
    public class AbstractSocketCommandTests
    {
        [Fact]
        public void SetParameters_MalformedJson_ThrowsMalformedSocketCommandParametersExceptionWrappingTheJsonException()
        {
            var command = new StubParamsCommand { Parameters = new StubParams() };

            var ex = Assert.Throws<MalformedSocketCommandParametersException>(() => command.SetParameters("{not valid json"));

            Assert.IsType<JsonException>(ex.InnerException);
            Assert.Contains(nameof(StubParamsCommand), ex.Message);
        }

        [Fact]
        public void SetParameters_NullParameters_ThrowsMalformedSocketCommandParametersExceptionWrappingArgumentNullException()
        {
            var command = new StubParamsCommand { Parameters = new StubParams() };

            var ex = Assert.Throws<MalformedSocketCommandParametersException>(() => command.SetParameters(null));

            Assert.IsType<ArgumentNullException>(ex.InnerException);
        }

        [Fact]
        public void SetParameters_ValidJson_BindsParametersWithoutThrowing()
        {
            var command = new StubParamsCommand { Parameters = new StubParams() };

            command.SetParameters("{\"value\":\"hello\"}");

            Assert.Equal("hello", command.Parameters.Value);
        }

        [Fact]
        public void SetParameters_WithResponseData_MalformedJson_ThrowsMalformedSocketCommandParametersException()
        {
            // AbstractSocketCommand<TReturn, TParams> shares the same DeserializeParameters helper as
            // AbstractSocketCommandWithParams<T> above — verify it classifies identically.
            var command = new StubParamsWithResponseCommand { Parameters = new StubParams() };

            var ex = Assert.Throws<MalformedSocketCommandParametersException>(() => command.SetParameters("not json"));

            Assert.IsType<JsonException>(ex.InnerException);
        }

        private sealed class StubParams
        {
            public string Value { get; set; } = "";
        }

        private sealed class StubParamsCommand : AbstractSocketCommandWithParams<StubParams>
        {
            public override string Name { get; set; } = nameof(StubParamsCommand);

            public override Task<ApiSocketResponse> ExecuteAsync(SocketContext context, CancellationToken cancellationToken)
                => Task.FromResult(Success());
        }

        private sealed class StubParamsWithResponseCommand : AbstractSocketCommand<string, StubParams>
        {
            public override string Name { get; set; } = nameof(StubParamsWithResponseCommand);

            public override Task<ApiSocketResponse<string>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
                => Task.FromResult(Success("ok"));
        }
    }
}
