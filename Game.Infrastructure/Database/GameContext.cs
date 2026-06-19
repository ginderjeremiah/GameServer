using Game.Infrastructure.Entities;
using Game.Core;
using Microsoft.EntityFrameworkCore;
using Attribute = Game.Infrastructure.Entities.Attribute;

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

#nullable disable
        public DbSet<AppliedMod> AppliedMods { get; set; }
        public DbSet<AttributeDistribution> AttributeDistributions { get; set; }
        public DbSet<BrowserInfo> BrowserInfos { get; set; }
        public DbSet<Device> Devices { get; set; }
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
        public DbSet<ItemModTag> ItemModTags { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<ItemModSlot> ItemModSlots { get; set; }
        public DbSet<ItemTag> ItemTags { get; set; }
        public DbSet<LogPreference> LogPreferences { get; set; }
        public DbSet<LogType> LogTypes { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<PlayerAttribute> PlayerAttributes { get; set; }
        public DbSet<PlayerChallenge> PlayerChallenges { get; set; }
        public DbSet<PlayerSkill> PlayerSkills { get; set; }
        public DbSet<PlayerStatistic> PlayerStatistics { get; set; }
        public DbSet<Rarity> Rarities { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Skill> Skills { get; set; }
        public DbSet<SkillDamageMultiplier> SkillDamageMultipliers { get; set; }
        public DbSet<SkillEffect> SkillEffects { get; set; }
        public DbSet<StatisticType> StatisticTypes { get; set; }
        public DbSet<ItemModType> ItemModTypes { get; set; }
        public DbSet<TagCategory> TagCategories { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<UnlockedItem> UnlockedItems { get; set; }
        public DbSet<UnlockedMod> UnlockedMods { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserLogin> UserLogins { get; set; }
        public DbSet<ZoneEnemy> ZoneEnemies { get; set; }
        public DbSet<Zone> Zones { get; set; }
#nullable restore

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
                    .HasIdentityOptions(0, 1, 0);

                entity.Property(i => i.Name)
                    .HasMaxLength(50);

                // Explicit join entity (ItemTag) backs the Item.Tags skip navigation so the admin tag-setting
                // path can add/remove a single assignment without loading a tag's full membership. Cascade is
                // load-bearing: hard-deleting an in-use tag (#297) relies on its join rows cascading away.
                entity.HasMany(i => i.Tags)
                    .WithMany()
                    .UsingEntity<ItemTag>(
                        r => r.HasOne<Tag>().WithMany().HasForeignKey(it => it.TagId).OnDelete(DeleteBehavior.Cascade),
                        l => l.HasOne<Item>().WithMany().HasForeignKey(it => it.ItemId).OnDelete(DeleteBehavior.Cascade),
                        j => j.HasKey(it => new { it.ItemId, it.TagId }));
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
                    .HasIdentityOptions(0, 1, 0);

                entity.Property(im => im.Name)
                    .HasMaxLength(50);

                // Explicit join entity (ItemModTag) backs the ItemMod.Tags skip navigation; see the Item.Tags
                // configuration above for the rationale and the load-bearing cascade.
                entity.HasMany(im => im.Tags)
                    .WithMany()
                    .UsingEntity<ItemModTag>(
                        r => r.HasOne<Tag>().WithMany().HasForeignKey(imt => imt.TagId).OnDelete(DeleteBehavior.Cascade),
                        l => l.HasOne<ItemMod>().WithMany().HasForeignKey(imt => imt.ItemModId).OnDelete(DeleteBehavior.Cascade),
                        j => j.HasKey(imt => new { imt.ItemModId, imt.TagId }));
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

                // EntityId is null for global statistics (e.g. EnemiesKilled). A default unique index treats
                // nulls as distinct, so concurrent at-least-once applies of the same global statistic would
                // never collide — defeating the write-behind handler's unique-violation-then-retry idempotency
                // and leaving duplicate rows. NULLS NOT DISTINCT makes (player, type, null) collide as intended.
                entity.HasIndex(ps => new { ps.PlayerId, ps.StatisticTypeId, ps.EntityId })
                    .IsUnique()
                    .AreNullsDistinct(false);

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

            modelBuilder.Entity<Role>(entity =>
            {
                entity.Property(r => r.Id)
                    .ValueGeneratedNever();

                entity.Property(r => r.Name)
                    .HasMaxLength(50);

                entity.HasMany(r => r.Users)
                    .WithMany(u => u.Roles)
                    .UsingEntity(join => join.ToTable("UserRoles"));

                entity.HasData(Enum.GetValues<ERole>().Select(r =>
                {
                    return new Role
                    {
                        Id = (int)r,
                        Name = r.ToString(),
                    };
                }));
            });

            modelBuilder.Entity<Skill>(entity =>
            {
                entity.Property(s => s.Id)
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

            modelBuilder.Entity<SkillEffect>(entity =>
            {
                entity.Property(se => se.Amount)
                    .HasPrecision(18, 3);

                entity.Property(se => se.ScalingAmount)
                    .HasPrecision(18, 3);

                // Two FKs to Attributes (the affected attribute and the scaling attribute) — configure both
                // explicitly so the convention doesn't have to disambiguate the matching nav/FK pairs.
                entity.HasOne(se => se.Attribute)
                    .WithMany()
                    .HasForeignKey(se => se.AttributeId);

                entity.HasOne(se => se.ScalingAttribute)
                    .WithMany()
                    .HasForeignKey(se => se.ScalingAttributeId);
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

                // Self-contained PBKDF2 hash: $pbkdf2-sha256$<iterations>$<base64-salt>$<base64-key>.
                entity.Property(u => u.PassHash)
                    .HasMaxLength(128);

                // At most one active (non-archived) account per username, enforced at the DB level so two
                // concurrent CreateAccount requests can't both insert a duplicate active row. The partial
                // filter excludes archived users, preserving username reuse after archival.
                entity.HasIndex(u => u.Username)
                    .IsUnique()
                    .HasFilter("\"ArchivedAt\" IS NULL");
            });

            modelBuilder.Entity<BrowserInfo>(entity =>
            {
                entity.Property(b => b.UserAgent)
                    .HasMaxLength(BrowserInfo.MaxUserAgentLength);

                entity.Property(b => b.SecChUa)
                    .HasMaxLength(BrowserInfo.MaxClientHintLength);

                entity.Property(b => b.SecChUaMobile)
                    .HasMaxLength(BrowserInfo.MaxClientHintLength);

                entity.Property(b => b.SecChUaPlatform)
                    .HasMaxLength(BrowserInfo.MaxClientHintLength);

                // Deduplicate browser profiles by their user-agent string.
                entity.HasIndex(b => b.UserAgent)
                    .IsUnique();
            });

            modelBuilder.Entity<Device>(entity =>
            {
                entity.Property(d => d.DeviceFingerprintHash)
                    .HasMaxLength(Device.MaxFingerprintLength);

                // Deduplicate devices by their client-computed fingerprint hash.
                entity.HasIndex(d => d.DeviceFingerprintHash)
                    .IsUnique();

                entity.HasOne(d => d.BrowserInfo)
                    .WithMany(b => b.Devices)
                    .HasForeignKey(d => d.BrowserInfoId);
            });

            modelBuilder.Entity<UserLogin>(entity =>
            {
                entity.Property(l => l.IpAddress)
                    .HasMaxLength(UserLogin.MaxIpAddressLength);

                // The (user, IP, device) combination is unique; its LastConnection is updated in place
                // rather than appending a new row per request.
                entity.HasIndex(l => new { l.UserId, l.IpAddress, l.DeviceId })
                    .IsUnique();

                entity.HasOne(l => l.User)
                    .WithMany()
                    .HasForeignKey(l => l.UserId);

                entity.HasOne(l => l.Device)
                    .WithMany(d => d.UserLogins)
                    .HasForeignKey(l => l.DeviceId);
            });

            modelBuilder.Entity<Zone>(entity =>
            {
                entity.Property(z => z.Id)
                    .HasIdentityOptions(0, 1, 0);

                entity.Property(z => z.Name)
                    .HasMaxLength(50);

                entity.Property(z => z.BossLevel)
                    .HasDefaultValue(1);

                // The dedicated boss is an optional reference to an enemy. Navigation-less FK: deleting the
                // referenced enemy clears the zone's boss (SetNull) rather than blocking the delete.
                entity.HasOne<Enemy>()
                    .WithMany()
                    .HasForeignKey(z => z.BossEnemyId)
                    .OnDelete(DeleteBehavior.SetNull);

                // The unlock challenge is an optional reference to a challenge. Navigation-less FK: deleting
                // the referenced challenge clears the gate (SetNull), leaving the zone always open, rather
                // than blocking the delete.
                entity.HasOne<Challenge>()
                    .WithMany()
                    .HasForeignKey(z => z.UnlockChallengeId)
                    .OnDelete(DeleteBehavior.SetNull);
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
                // Materialize once so the PK and FK branches share a single pre-mutation snapshot;
                // the PK branch clears IsTemporary, which the deferred predicate would otherwise re-filter on.
                var tempProps = entry.Properties.Where(p => p.IsTemporary).ToList();
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
            throw new NotSupportedException("Synchronous SaveChanges is intentionally unavailable; use SaveChangesAsync.");
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            throw new NotSupportedException("Synchronous SaveChanges is intentionally unavailable; use SaveChangesAsync.");
        }
    }
}
