namespace Game.Api.Models.Progress
{
    /// <summary>
    /// Pushed to the client when the player completes a challenge, so the newly-unlocked rewards become
    /// usable immediately (equip the item/mod) without a page refresh. The reward ids are null when the
    /// challenge carries no reward of that kind.
    /// </summary>
    public class ChallengeCompletedModel : IModel
    {
        public int ChallengeId { get; set; }
        public int? RewardItemId { get; set; }
        public int? RewardItemModId { get; set; }
    }
}
