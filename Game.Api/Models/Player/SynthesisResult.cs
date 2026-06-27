using Game.Abstractions;

namespace Game.Api.Models.Player
{
    /// <summary>
    /// The outcome of a <c>SynthesizeSkill</c> command. On success <see cref="ResultSkillId"/> is the id of the
    /// skill the recipe produced — now unlocked and unselected; the client adds it to its loadout pool from its
    /// cached skills reference set. Synthesis is player-initiated, so this command response <em>is</em> the
    /// delivery — there is no separate server push (spike #1125). Null on a rejected synthesis, where the
    /// response's <c>Error</c> carries the reason.
    /// </summary>
    public class SynthesisResult : IModel
    {
        public int? ResultSkillId { get; set; }
    }
}
