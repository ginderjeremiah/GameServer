using Game.Api.Models.Common;
using Game.Api.Models.ReferenceData;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Returns the current content version of every reference-data set in a single round-trip.
    /// The loading screen calls this first and re-fetches only the sets whose version differs from
    /// the one stored alongside its cached copy, resolving the rest from local storage.
    /// </summary>
    /// <remarks>
    /// The set is sourced from the registered <see cref="IReferenceDataCommand"/>s (the <c>Get*</c>
    /// commands), so a new reference-data command is automatically versioned with no change here.
    /// </remarks>
    public class GetReferenceDataVersions : AbstractSocketCommandWithResponseData<IEnumerable<ReferenceDataVersion>>
    {
        private readonly IEnumerable<IReferenceDataCommand> _referenceDataCommands;

        public override string Name { get; set; } = nameof(GetReferenceDataVersions);

        public GetReferenceDataVersions(IEnumerable<IReferenceDataCommand> referenceDataCommands)
        {
            _referenceDataCommands = referenceDataCommands;
        }

        public override Task<ApiSocketResponse<IEnumerable<ReferenceDataVersion>>> HandleExecuteAsync(SocketContext context, CancellationToken cancellationToken)
        {
            var versions = _referenceDataCommands
                .Select(command => new ReferenceDataVersion
                {
                    Command = command.Name,
                    Version = command.ComputeVersion()
                })
                .OrderBy(version => version.Command, StringComparer.Ordinal)
                .ToList();

            return Task.FromResult(Success<IEnumerable<ReferenceDataVersion>>(versions));
        }
    }
}
