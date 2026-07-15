namespace Game.Api.Models.Enemies
{
    public class BattleLostRequest
    {
        /// <summary>
        /// The total battle duration (ms) the client simulated for this loss. Diagnostic only — it is
        /// not validated as anti-cheat; the server logs when it diverges from its own parity replay so a
        /// front/back battle-logic drift is visible. Null when the client did not report a duration.
        /// </summary>
        public int? ClientTotalMs { get; set; }
    }
}
