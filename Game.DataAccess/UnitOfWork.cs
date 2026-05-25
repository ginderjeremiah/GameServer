using Game.Abstractions.Entities;
using Game.Application;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Game.DataAccess
{
    internal class UnitOfWork(GameContext context) : IUnitOfWork
    {
        public Task CommitAsync()
        {
            foreach (var entry in context.ChangeTracker.Entries())
            {
                var tempProps = entry.Properties.Where(p => p.IsTemporary);
                if (entry.State is not EntityState.Added)
                {
                    var idProp = tempProps.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
                    if (idProp is not null && entry.Entity is IZeroBasedIdentityEntity zbe && zbe.Id == 0)
                    {
                        idProp.IsTemporary = false;
                        idProp.CurrentValue = 0;
                    }
                }

                var fkProps = tempProps.Where(p => p.Metadata.IsForeignKey() && p.Metadata.ClrType == typeof(int)).ToList();
                if (fkProps.Count != 0)
                {
                    foreach (var fkProp in fkProps)
                    {
                        var navigation = fkProp.Metadata.GetContainingForeignKeys()
                            .FirstOrDefault(fk => fk.DeclaringEntityType == fkProp.Metadata.DeclaringType);

                        if (navigation is not null && navigation.PrincipalEntityType.ClrType.IsAssignableTo(typeof(IZeroBasedIdentityEntity)))
                        {
                            fkProp.IsTemporary = false;
                            fkProp.CurrentValue = 0;
                        }
                    }
                }
            }

            return context.SaveChangesAsync();
        }
    }
}
