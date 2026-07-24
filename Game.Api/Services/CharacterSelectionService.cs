using Game.Api.Models.Auth;
using Game.Api.Models.Player;
using Game.Application.Services;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Game.Api.Services
{
    /// <summary>
    /// Result of <see cref="CharacterSelectionService.SelectPlayer"/>/<see cref="CharacterSelectionService.SwitchPlayer"/>:
    /// a status plus, on success, the rotated token pair and the projected player payload. Mirrors the
    /// status-plus-nullable-payload shape <see cref="Game.Application.Services.AccountSelectPlayerResult"/> uses.
    /// </summary>
    public record CharacterSelectionOutcome(SelectPlayerStatus Status, AuthTokenPair? Tokens, PlayerData? Player)
    {
        [MemberNotNullWhen(true, nameof(Tokens), nameof(Player))]
        public bool Success => Status == SelectPlayerStatus.Success;

        public static CharacterSelectionOutcome Succeeded(AuthTokenPair tokens, PlayerData player)
        {
            return new CharacterSelectionOutcome(SelectPlayerStatus.Success, tokens, player);
        }

        public static CharacterSelectionOutcome Failed(SelectPlayerStatus status)
        {
            return new CharacterSelectionOutcome(status, null, null);
        }
    }

    /// <summary>
    /// Orchestrates the character-selection and in-game character-switch flows so
    /// <see cref="Controllers.PlayersController"/> stays limited to request validation and response mapping
    /// (#2057). Lives in the API layer (rather than folded into <see cref="AccountService"/>) because it
    /// depends on request-scoped, presentation-tier state — the token-derived <see cref="SessionService"/>
    /// and the socket-presence <see cref="SocketManagerService"/> — that the application layer must not
    /// reference.
    /// </summary>
    public class CharacterSelectionService(
        SessionService sessionService,
        SessionInitializer sessionInitializer,
        AccountService accountService,
        SocketManagerService socketManager,
        PlayerService playerService,
        OfflineProgressService offlineProgressService,
        PlayerDataAssembler playerDataAssembler,
        ILogger<CharacterSelectionService> logger)
    {
        private readonly SessionService _sessionService = sessionService;
        private readonly SessionInitializer _sessionInitializer = sessionInitializer;
        private readonly AccountService _accountService = accountService;
        private readonly SocketManagerService _socketManager = socketManager;
        private readonly PlayerService _playerService = playerService;
        private readonly OfflineProgressService _offlineProgressService = offlineProgressService;
        private readonly PlayerDataAssembler _playerDataAssembler = playerDataAssembler;
        private readonly ILogger<CharacterSelectionService> _logger = logger;

        /// <summary>
        /// Selects which of the authenticated account's characters to enter as. Validates ownership
        /// (anti-cheat), binds the session, and rotates the token pair to carry the chosen player so every
        /// later request (and the socket handshake) resolves it from the token. The active-session takeover
        /// check is made by the client after this step, since it is a per-player presence check.
        /// </summary>
        public async Task<CharacterSelectionOutcome> SelectPlayer(int userId, int playerId, string refreshToken, CancellationToken cancellationToken)
        {
            var result = await _accountService.SelectPlayer(userId, playerId, refreshToken, cancellationToken);
            if (!result.Success)
            {
                return CharacterSelectionOutcome.Failed(result.Status);
            }

            // Establish the session binding now that a character is chosen — a request-scoped presentation
            // concern, mirroring how login used to bind. The token returned already carries the player.
            await _sessionService.CreateSession(userId, result.Player.Id, cancellationToken);

            var playerData = await _playerDataAssembler.Build(result.Player, cancellationToken);
            return CharacterSelectionOutcome.Succeeded(result.Tokens, playerData);
        }

        /// <summary>
        /// Switches the authenticated account's live character without re-logging in (spike #922). Credits the
        /// departed character — the one the caller's token currently binds — for any elapsed idle time via the
        /// offline-rewards simulator, resolving its in-flight battle and dropping the login-time 5-minute floor
        /// so a deliberate switch loses no progress, then binds and loads the newly selected character through
        /// the same path as <see cref="SelectPlayer"/> (validating ownership and rotating the token). The client
        /// must tear down its game socket before calling this, since the departed-character credit runs over
        /// HTTP, off that character's battle loop; the credit is skipped server-side if that socket is still
        /// live (or claimed by another in-flight credit), and a socket that registers while the credit is
        /// running defers behind its claim (#2041), so a misbehaving client cannot race the credit against the
        /// live loop's saves.
        /// </summary>
        public async Task<CharacterSelectionOutcome> SwitchPlayer(int userId, int playerId, string refreshToken, CancellationToken cancellationToken)
        {
            // Validate the switch target up front (read-only, no token rotation) so an unowned switch never
            // credits or mutates the departed character. This repeats the ownership (GetPlayerIds) read that
            // SelectPlayer above performs authoritatively — the duplicate is deliberate: only this early gate can
            // stop the departed-character credit before it runs. SelectPlayer above re-validates and may also fail
            // on an invalid refresh token, in which case the departed character has been credited and re-anchored
            // (benign: the progress is legitimate and re-anchoring makes a retry near-idempotent).
            if (!await _accountService.OwnsPlayer(userId, playerId, cancellationToken))
            {
                return CharacterSelectionOutcome.Failed(SelectPlayerStatus.NotOwned);
            }

            // Settle the departed character before binding the new one, so its idle progress and in-flight
            // battle are credited rather than discarded when the session is rebound below.
            await CreditDepartedCharacter(playerId, cancellationToken);

            return await SelectPlayer(userId, playerId, refreshToken, cancellationToken);
        }

        /// <summary>
        /// Credits the character the caller is switching away from (the token's currently-selected player) for
        /// any elapsed idle time, resolving its in-flight battle. A no-op when there is no departed character to
        /// settle — a pre-selection token (no bound player) or a switch to the same character — when that
        /// character can no longer be loaded, or when its presence key is already claimed (a live socket, or
        /// another switch-away credit already in flight — see the atomic claim below).
        /// </summary>
        private async Task CreditDepartedCharacter(int targetPlayerId, CancellationToken cancellationToken)
        {
            if (_sessionService.TokenSelectedPlayerId is not int departedPlayerId || departedPlayerId == targetPlayerId)
            {
                return;
            }

            // The credit is a read-modify-write against the departed character's aggregate, run here over HTTP
            // off its battle loop. If that character still has a live socket, its battle-completion commands are
            // mutating the same cached aggregate under the per-socket command lock — crediting it here would
            // reintroduce the exact lost-update race that lock exists to prevent. The client is expected to tear
            // its game socket down before switching, so a live socket means a misbehaving/malicious client.
            //
            // The check-and-reserve happens atomically (SocketManagerService.TryClaimForSwitchCredit) rather
            // than a plain presence read followed by the credit: a socket that registers concurrently either
            // wins outright (the claim below fails and the credit is skipped, exactly as an already-live socket
            // is) or defers behind this claim (SocketManagerService.RegisterSocket) until it's released below —
            // closing the gap where a socket opening between a read and the credit that followed it could still
            // race its own battle loop's saves against this HTTP-thread credit (#2041).
            var claimValue = await _socketManager.TryClaimForSwitchCredit(departedPlayerId);
            if (claimValue is null)
            {
                _logger.LogWarning(
                    "Skipping the switch-away credit for player {DepartedPlayerId}: it still has a live socket (or another switch-away credit is already claiming it), so its battle loop owns its saves. The client should close its game socket before switching.",
                    departedPlayerId);
                return;
            }

            try
            {
                // Bind the departed character's in-flight session state (from the token claim) so the simulator
                // can resolve any stale battle, then load its aggregate to apply the credited rewards to.
                await _sessionInitializer.EnsureSessionLoaded(cancellationToken);
                var departed = await _playerService.LoadPlayer(departedPlayerId, cancellationToken);
                if (departed is null)
                {
                    return;
                }

                await _offlineProgressService.SimulateSwitchProgress(departed, _sessionService.PlayerState, cancellationToken);
            }
            finally
            {
                // Release even on failure/cancellation so a faulted credit doesn't wedge a reconnect for the
                // full claim TTL — RegisterSocket's own bound would still recover it, but there's no reason to
                // make a legitimate reconnect wait that long when we can release promptly instead.
                await _socketManager.ReleaseSwitchCreditClaim(departedPlayerId, claimValue);
            }
        }
    }
}
