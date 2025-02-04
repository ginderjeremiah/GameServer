namespace Game.Abstractions.Entities
{
    public interface IAuditableEntity
    {
        public DateTime CreatedOn { get; set; }
        public DateTime ModifiedOn { get; set; }
    }
}
