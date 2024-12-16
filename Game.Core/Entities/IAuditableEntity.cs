namespace Game.Core.Entities
{
    public interface IAuditableEntity
    {
        public DateTime CreatedOn { get; set; }
        public DateTime ModifiedOn { get; set; }
    }
}
