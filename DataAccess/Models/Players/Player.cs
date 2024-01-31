namespace DataAccess.Models.Players
{
    public class Player
    {
        public int PlayerId { get; set; }
        public string UserName { get; set; }
        public Guid Salt { get; set; }
        public string PassHash { get; set; }
        public string PlayerName { get; set; }
        public int Level { get; set; }
        public int Exp { get; set; }
        public int StatPointsGained { get; set; }
        public int StatPointsUsed { get; set; }
    }
}
