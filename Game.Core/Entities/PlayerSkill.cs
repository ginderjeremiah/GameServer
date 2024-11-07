using System.Text.Json.Serialization;

namespace Game.Core.Entities
{
    public partial class PlayerSkill
    {
        public int PlayerId { get; set; }
        public int SkillId { get; set; }
        public bool Selected { get; set; }

        [JsonIgnore]
        public virtual Player Player { get; set; }
        public virtual Skill Skill { get; set; }
    }
}
