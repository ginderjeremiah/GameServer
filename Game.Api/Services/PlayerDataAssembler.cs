using Game.Abstractions.Contracts;
using Game.Abstractions.DataAccess;
using Game.Api.Models.Player;
using Game.Application.Services;
using Game.Core.Players;

namespace Game.Api.Services
{
    /// <summary>
    /// Projects a loaded <see cref="Player"/> aggregate to its wire DTO, resolving its class's attribute
    /// distributions (the locked-base fingerprint #1126 area D) so the client battler composes the same
    /// locked base the backend snapshot does. Shared by every endpoint that returns a full player payload
    /// (auth's Status, and the character-selection flow's SelectPlayer/SwitchPlayer).
    /// </summary>
    public class PlayerDataAssembler(IClasses classes, BattleService battleService)
    {
        private readonly IClasses _classes = classes;
        private readonly BattleService _battleService = battleService;

        // A player's ClassId is validated at creation, so an unresolvable class here is a corrupt cache,
        // not a bad request — fail loudly (matching BattleService.ResolveClass and AccountService's creatable-
        // class resolve) rather than serving an empty fingerprint, which would load fine but 500 every battle.
        public async Task<PlayerData> Build(Player player, CancellationToken cancellationToken)
        {
            var @class = _classes.GetClass(player.ClassId)
                ?? throw new InvalidOperationException(
                    $"Class {player.ClassId} for player {player.Id} could not be resolved from the catalogue.");

            var lockedBaseDistribution = @class.AttributeDistributions
                .Select(distribution => new AttributeDistribution
                {
                    AttributeId = distribution.AttributeId,
                    BaseAmount = distribution.BaseAmount,
                    AmountPerLevel = distribution.AmountPerLevel,
                })
                .ToList();

            var passive = @class.SignaturePassive;
            var signaturePassive = new SignaturePassive
            {
                AttributeId = passive.Attribute,
                Amount = passive.Amount,
                ScalingAttributeId = passive.ScalingAttribute,
                ScalingAmount = passive.ScalingAmount,
                ModifierType = passive.ModifierType,
            };
            var rating = await _battleService.RatePlayer(player, cancellationToken);
            return PlayerData.FromPlayer(player, lockedBaseDistribution, signaturePassive, rating);
        }
    }
}
