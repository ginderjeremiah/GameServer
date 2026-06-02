using Game.Abstractions.Entities;
using Game.Core;
using Microsoft.EntityFrameworkCore;
using Attribute = Game.Abstractions.Entities.Attribute;

namespace Game.Infrastructure.Database
{
    /// <summary>
    /// <para>An extension of <see cref="DbContext"/> containing a <see cref="DbSet{TEntity}"/> for each entity used by the game.</para>
    /// <inheritdoc/>
    /// </summary>
    /// <inheritdoc/>
    public class GameContext : DbContext
    {
        /// <inheritdoc cref="DbContext(DbContextOptions)"/>
        public GameContext(DbContextOptions<GameContext> options) : base(options) { }

        public DbSet<AppliedMod> AppliedMods { get; set; }
        public DbSet<AttributeDistribution> AttributeDistributions { get; set; }
        public DbSet<Attribute> Attributes { get; set; }
        public DbSet<Challenge> Challenges { get; set; }
        public DbSet<ChallengeType> ChallengeTypes { get; set; }
        public DbSet<Enemy> Enemies { get; set; }
        public DbSet<EnemySkill> EnemySkills { get; set; }
        public DbSet<EquipmentSlot> EquipmentSlots { get; set; }
        public DbSet<ItemAttribute> ItemAttributes { get; set; }
        public DbSet<ItemCategory> ItemCategories { get; set; }
        public DbSet<ItemModAttribute> ItemModAttributes { get; set; }
        public DbSet<ItemMod> ItemMods { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<ItemModSlot> ItemModSlots { get; set; }
        public DbSet<LogPreference> LogPreferences { get; set; }
        public DbSet<LogType> LogTypes { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<PlayerAttribute> PlayerAttributes { get; set; }
        public DbSet<PlayerChallenge> PlayerChallenges { get; set; }
        public DbSet<PlayerSkill> PlayerSkills { get; set; }
        public DbSet<PlayerStatistic> PlayerStatistics { get; set; }
        public DbSet<Rarity> Rarities { get; set; }
        public DbSet<Skill> Skills { get; set; }
        public DbSet<SkillDamageMultiplier> SkillDamageMultipliers { get; set; }
        public DbSet<StatisticType> StatisticTypes { get; set; }
        public DbSet<ItemModType> ItemModTypes { get; set; }
        public DbSet<TagCategory> TagCategories { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<UnlockedItem> UnlockedItems { get; set; }
        public DbSet<UnlockedMod> UnlockedMods { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<ZoneEnemy> ZoneEnemies { get; set; }
        public DbSet<Zone> Zones { get; set; }

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AppliedMod>(entity =>
            {
                entity.HasKey(am => new { am.PlayerId, am.ItemId, am.ItemModSlotId });
            });

            modelBuilder.Entity<Attribute>(entity =>
            {
                entity.Property(a => a.Id)
                    .ValueGeneratedNever();

                entity.Property(a => a.Name)
                    .HasMaxLength(50);

                entity.HasData(Core.Attributes.Attribute.GetAllAttributes().Select(a =>
                {
                    return new Attribute
                    {
                        Id = (int)a.Id,
                        Name = a.Name,
                        Description = a.Description,
                    };
                }));
            });

            modelBuilder.Entity<AttributeDistribution>(entity =>
            {
                entity.HasKey(ad => new { ad.EnemyId, ad.AttributeId });

                entity.Property(ad => ad.AmountPerLevel)
                    .HasPrecision(18, 3);

                entity.Property(ad => ad.BaseAmount)
                    .HasPrecision(18, 3);
            });

            modelBuilder.Entity<Challenge>(entity =>
            {
                entity.Property(c => c.Id)
                    .UseIdentityColumn(0, 1)
                    .HasIdentityOptions(0, 1, 0);

                entity.Property(c => c.Name)
                    .HasMaxLength(100);

                entity.Property(c => c.Description)
                    .HasMaxLength(500);

                entity.Property(c => c.ProgressGoal)
                    .HasPrecision(36, 3);
            });

            modelBuilder.Entity<ChallengeType>(entity =>
            {
                entity.Property(ct => ct.Id)
                    .ValueGeneratedNever();

                entity.Property(ct => ct.Name)
                    .HasMaxLength(100);

                entity.HasData(Core.Progress.ChallengeType.GetAll().Select(type =>
                {
                    return new ChallengeType
                    {
                        Id = (int)type.Id,
                        Name = type.Name,
                        StatisticTypeId = (int?)type.StatisticType?.Id,
                    };
                }));
            });

            modelBuilder.Entity<Enemy>(entity =>
            {
                entity.Property(e => e.Id)
                    .UseIdentityColumn(0, 1)
                    .HasIdentityOptions(0, 1, 0);

                entity.Property(e => e.Name)
                    .HasMaxLength(50);
            });

            modelBuilder.Entity<EnemySkill>()
                .HasKey(es => new { es.EnemyId, es.SkillId });

            modelBuilder.Entity<EquipmentSlot>(entity =>
            {
                entity.Property(es => es.Id)
                    .ValueGeneratedNever();

                entity.Property(es => es.Name)
                    .HasMaxLength(50);

                entity.HasData(Enum.GetValues<EEquipmentSlot>().Select(a =>
                {
                    var equipmentSlot = new Core.Players.Inventories.EquipmentSlot(a);
                    return new EquipmentSlot
                    {
                        Id = (int)a,
                        Name = equipmentSlot.Name,
                        ItemCategoryId = (int)equipmentSlot.ItemCategory,
                    };
                }));
            });

            modelBuilder.Entity<Item>(entity =>
            {
                entity.Property(i => i.Id)
                    .UseIdentityColumn(0, 1)
                    .HasIdentityOptions(0, 1, 0);

                entity.Property(i => i.Name)
                    .HasMaxLength(50);

                entity.HasMany(i => i.Tags)
                    .WithMany(t => t.Items)
                    .UsingEntity(join => join.ToTable("ItemTags"));
            });

            modelBuilder.Entity<ItemAttribute>(entity =>
            {
                entity.HasKey(ia => new { ia.ItemId, ia.AttributeId });

                entity.Property(ia => ia.Amount)
                    .HasPrecision(18, 3);
            });

            modelBuilder.Entity<ItemCategory>(entity =>
            {
                entity.Property(ic => ic.Id)
                    .ValueGeneratedNever();

                entity.Property(ic => ic.Name)
                    .HasMaxLength(50);

                entity.HasData(Enum.GetValues<EItemCategory>().Select(a =>
                {
                    return new ItemCategory
                    {
                        Id = (int)a,
                        Name = a.ToString().Capitalize().SpaceWords(),
                    };
                }));
            });

            modelBuilder.Entity<ItemMod>(entity =>
            {
                entity.Property(im => im.Id)
                    .UseIdentityColumn(0, 1)
                    .HasIdentityOptions(0, 1, 0);

                entity.Property(im => im.Name)
                    .HasMaxLength(50);

                entity.HasMany(im => im.Tags)
                    .WithMany(t => t.ItemMods)
                    .UsingEntity(join => join.ToTable("ItemModTags"));
            });

            modelBuilder.Entity<ItemModAttribute>(entity =>
            {
                entity.HasKey(ima => new { ima.ItemModId, ima.AttributeId });

                entity.Property(ima => ima.Amount)
                    .HasPrecision(18, 3);
            });

            modelBuilder.Entity<LogPreference>()
                .HasKey(lp => new { lp.PlayerId, lp.LogTypeId });

            modelBuilder.Entity<LogType>(entity =>
            {
                entity.Property(ls => ls.Id)
                    .ValueGeneratedNever();

                entity.Property(ls => ls.Name)
                    .HasMaxLength(20);

                entity.HasData(Enum.GetValues<ELogType>().Select(a =>
                {
                    return new LogType
                    {
                        Id = (int)a,
                        Name = a.ToString().Capitalize().SpaceWords(),
                    };
                }));
            });

            modelBuilder.Entity<Player>(entity =>
            {
                entity.Property(p => p.Name)
                    .HasMaxLength(20);
            });

            modelBuilder.Entity<PlayerChallenge>(entity =>
            {
                entity.HasKey(pc => new { pc.PlayerId, pc.ChallengeId });

                entity.Property(c => c.Progress)
                    .HasPrecision(36, 3);
            });

            modelBuilder.Entity<PlayerSkill>()
                .HasKey(ps => new { ps.PlayerId, ps.SkillId });

            modelBuilder.Entity<PlayerAttribute>(entity =>
            {
                entity.HasKey(pa => new { pa.PlayerId, pa.AttributeId });

                entity.Property(pa => pa.Amount)
                    .HasPrecision(18, 3);
            });

            modelBuilder.Entity<PlayerStatistic>(entity =>
            {
                entity.HasKey(ps => ps.Id);

                entity.HasIndex(ps => new { ps.PlayerId, ps.StatisticTypeId, ps.EntityId })
                    .IsUnique();

                entity.Property(c => c.Value)
                    .HasPrecision(36, 3);
            });

            modelBuilder.Entity<Rarity>(entity =>
            {
                entity.Property(r => r.Id)
                    .ValueGeneratedNever();

                entity.Property(r => r.Name)
                    .HasMaxLength(50);

                entity.HasData(Enum.GetValues<ERarity>().Select(r =>
                {
                    var rarity = new Core.Rarity.Rarity(r);
                    return new Rarity
                    {
                        Id = (int)rarity.Id,
                        Name = rarity.Name,
                    };
                }));
            });

            modelBuilder.Entity<Skill>(entity =>
            {
                entity.Property(s => s.Id)
                    .UseIdentityColumn(0, 1)
                    .HasIdentityOptions(0, 1, 0);

                entity.Property(s => s.BaseDamage)
                    .HasPrecision(18, 3);

                entity.Property(s => s.Name)
                    .HasMaxLength(50);

                entity.Property(s => s.IconPath)
                    .HasMaxLength(50);
            });

            modelBuilder.Entity<SkillDamageMultiplier>(entity =>
            {
                entity.HasKey(sdm => new { sdm.SkillId, sdm.AttributeId });

                entity.Property(sdm => sdm.Multiplier)
                    .HasPrecision(18, 3);
            });

            modelBuilder.Entity<StatisticType>(entity =>
            {
                entity.Property(st => st.Id)
                    .ValueGeneratedNever();

                entity.Property(st => st.Name)
                    .HasMaxLength(100);

                entity.HasData(Core.Progress.StatisticType.GetAll().Select(type =>
                    new StatisticType
                    {
                        Id = (int)type.Id,
                        Name = type.Name,
                        EntityType = (int)type.EntityType,
                    }));
            });

            modelBuilder.Entity<ItemModType>(entity =>
            {
                entity.Property(st => st.Id)
                    .ValueGeneratedNever();

                entity.Property(st => st.Name)
                    .HasMaxLength(50);

                entity.HasData(Enum.GetValues<EItemModType>().Select(a =>
                {
                    return new ItemModType
                    {
                        Id = (int)a,
                        Name = a.ToString().Capitalize().SpaceWords(),
                    };
                }));
            });

            modelBuilder.Entity<Tag>()
                .Property(t => t.Name)
                .HasMaxLength(50);

            modelBuilder.Entity<TagCategory>(entity =>
            {
                entity.Property(tc => tc.Id)
                    .ValueGeneratedNever();

                entity.Property(tc => tc.Name)
                .HasMaxLength(50);

                entity.HasData(Enum.GetValues<ETagCategory>().Select(a =>
                {
                    return new TagCategory
                    {
                        Id = (int)a,
                        Name = a.ToString().Capitalize().SpaceWords(),
                    };
                }));
            });

            modelBuilder.Entity<UnlockedItem>(entity =>
            {
                entity.HasKey(ui => new { ui.PlayerId, ui.ItemId });
            });

            modelBuilder.Entity<UnlockedMod>(entity =>
            {
                entity.HasKey(um => new { um.PlayerId, um.ItemModId });
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(u => u.Username)
                    .HasMaxLength(20);

                entity.Property(u => u.PassHash)
                    .HasMaxLength(88);
            });

            modelBuilder.Entity<Zone>(entity =>
            {
                entity.Property(z => z.Id)
                    .UseIdentityColumn(0, 1)
                    .HasIdentityOptions(0, 1, 0);

                entity.Property(z => z.Name)
                    .HasMaxLength(50);
            });

            modelBuilder.Entity<ZoneEnemy>()
                .HasKey(ze => new { ze.ZoneId, ze.EnemyId });
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries())
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

            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        public override int SaveChanges()
        {
            throw new NotImplementedException();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            throw new NotImplementedException();
        }
    }
}
