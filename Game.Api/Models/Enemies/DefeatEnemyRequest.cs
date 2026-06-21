namespace Game.Api.Models.Enemies
{
    public class DefeatEnemyRequest
    {
        /// <summary>
        /// The total battle duration (ms) the client simulated for this victory. Diagnostic only — it is
        /// not validated as anti-cheat; the server logs when it diverges from its own parity replay so a
        /// front/back battle-logic drift is visible. Null when the client did not report a duration.
        /// </summary>
        public int? ClientTotalMs { get; set; }
    }
}
