using Game.Core;
using Game.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Attribute = Game.Core.Entities.Attribute;

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

        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<AttributeDistribution> AttributeDistributions { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<Attribute> Attributes { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<Enemy> Enemies { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<EnemyDrop> EnemyDrops { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<EnemySkill> EnemySkills { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<EquipmentSlot> EquipmentSlots { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<InventoryItemMod> InventoryItemMods { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<InventoryItem> InventoryItems { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<ItemAttribute> ItemAttributes { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<ItemCategory> ItemCategories { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<ItemModAttribute> ItemModAttributes { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<ItemMod> ItemMods { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<Item> Items { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<ItemModSlot> ItemModSlots { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<LogPreference> LogPreferences { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<LogSetting> LogSettings { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<Player> Players { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<PlayerAttribute> PlayerAttributes { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<PlayerSkill> PlayerSkills { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<Skill> Skills { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<SkillDamageMultiplier> SkillDamageMultipliers { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<ItemModSlotType> ItemModSlotTypes { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<TagCategory> TagCategories { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<Tag> Tags { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<ZoneEnemy> ZoneEnemies { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<ZoneEnemyAlias> ZoneEnemyAliases { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<ZoneEnemyProbability> ZoneEnemyProbabilities { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<Zone> Zones { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<ZoneDrop> ZoneDrops { get; set; }

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Attribute>()
                .Property(a => a.Id)
                .ValueGeneratedNever();

            modelBuilder.Entity<Attribute>()
                .HasEnumValues<Attribute, EAttribute>();

            modelBuilder.Entity<Attribute>()
                .Property(a => a.Name)
                .HasMaxLength(50);

            modelBuilder.Entity<AttributeDistribution>()
                .HasKey(ad => new { ad.EnemyId, ad.AttributeId });

            modelBuilder.Entity<AttributeDistribution>()
                .Property(ad => ad.AmountPerLevel)
                .HasPrecision(18, 3);

            modelBuilder.Entity<AttributeDistribution>()
                .Property(ad => ad.BaseAmount)
                .HasPrecision(18, 3);

            modelBuilder.Entity<Enemy>()
                .Property(e => e.Id)
                .UseIdentityColumn(0, 1);

            modelBuilder.Entity<Enemy>()
                .Property(e => e.Name)
                .HasMaxLength(50);

            modelBuilder.Entity<EnemySkill>()
                .HasKey(es => new { es.EnemyId, es.SkillId });

            modelBuilder.Entity<EnemyDrop>()
                .Property(ed => ed.DropRate)
                .HasPrecision(9, 8);

            modelBuilder.Entity<EquipmentSlot>()
                .Property(es => es.Id)
                .ValueGeneratedNever();

            modelBuilder.Entity<EquipmentSlot>()
                .Property(es => es.Name)
                .HasMaxLength(50);

            modelBuilder.Entity<EquipmentSlot>()
                .HasEnumValues<EquipmentSlot, EEquipmentSlot>();

            modelBuilder.Entity<InventoryItemMod>()
                .HasKey(iim => new { iim.InventoryItemId, iim.ItemModId });

            //Fix to prevent double cascading delete on SlotType => ItemSlot/ItemMod
            modelBuilder.Entity<InventoryItemMod>()
                .HasOne(iim => iim.ItemModSlot)
                .WithMany(isl => isl.InventoryItemMods)
                .OnDelete(DeleteBehavior.NoAction);

            //Do not enforce this constraint as it makes it difficult to modify data.
            //modelBuilder.Entity<InventoryItem>()
            //    .HasIndex(ii => new { ii.PlayerId, ii.Equipped, ii.InventorySlotNumber })
            //    .IsUnique();

            modelBuilder.Entity<Item>()
                .Property(i => i.Id)
                .UseIdentityColumn(0, 1);

            modelBuilder.Entity<Item>()
                .Property(i => i.Name)
                .HasMaxLength(50);

            modelBuilder.Entity<Item>()
                .Property(i => i.IconPath)
                .HasMaxLength(50);

            modelBuilder.Entity<Item>()
                .HasMany(i => i.Tags)
                .WithMany(t => t.Items)
                .UsingEntity(join => join.ToTable("ItemTags"));

            modelBuilder.Entity<ItemAttribute>()
                .HasKey(ia => new { ia.ItemId, ia.AttributeId });

            modelBuilder.Entity<ItemAttribute>()
                .Property(ia => ia.Amount)
                .HasPrecision(18, 3);

            modelBuilder.Entity<ItemCategory>()
                .Property(ic => ic.Id)
                .ValueGeneratedNever();

            modelBuilder.Entity<ItemCategory>()
                .HasEnumValues<ItemCategory, EItemCategory>();

            modelBuilder.Entity<ItemCategory>()
                .Property(ic => ic.Name)
                .HasMaxLength(50);

            modelBuilder.Entity<ItemMod>()
                .Property(im => im.Id)
                .UseIdentityColumn(0, 1);

            modelBuilder.Entity<ItemMod>()
                .Property(im => im.Name)
                .HasMaxLength(50);

            modelBuilder.Entity<ItemMod>()
                .HasMany(im => im.Tags)
                .WithMany(t => t.ItemMods)
                .UsingEntity(join => join.ToTable("ItemModTags"));

            modelBuilder.Entity<ItemModAttribute>()
                .HasKey(ima => new { ima.ItemModId, ima.AttributeId });

            modelBuilder.Entity<ItemModAttribute>()
                .Property(ima => ima.Amount)
                .HasPrecision(18, 3);

            modelBuilder.Entity<ItemModSlot>()
                .Property(isl => isl.Probability)
                .HasPrecision(9, 8);

            modelBuilder.Entity<ItemModSlot>()
                .Property(isl => isl.GuaranteedItemModId)
                .IsRequired(false);

            //Fix to prevent "possible" circular constraint when SlotType is deleted.
            modelBuilder.Entity<ItemModSlot>()
                .HasOne(isl => isl.GuaranteedItemMod)
                .WithMany(im => im.GuaranteedSlots)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LogPreference>()
                .HasKey(lp => new { lp.PlayerId, lp.LogSettingId });

            modelBuilder.Entity<LogSetting>()
                .Property(ls => ls.Id)
                .ValueGeneratedNever();

            modelBuilder.Entity<LogSetting>()
                .HasEnumValues<LogSetting, ELogSetting>();

            modelBuilder.Entity<LogSetting>()
                .Property(ls => ls.Name)
                .HasMaxLength(20);

            modelBuilder.Entity<Player>()
                .Property(p => p.UserName)
                .HasMaxLength(20);

            modelBuilder.Entity<Player>()
                .Property(p => p.Name)
                .HasMaxLength(20);

            modelBuilder.Entity<Player>()
                .Property(p => p.PassHash)
                .HasMaxLength(88);

            modelBuilder.Entity<PlayerSkill>()
                .HasKey(ps => new { ps.PlayerId, ps.SkillId });

            modelBuilder.Entity<PlayerAttribute>()
                .HasKey(pa => new { pa.PlayerId, pa.AttributeId });

            modelBuilder.Entity<PlayerAttribute>()
                .Property(pa => pa.Amount)
                .HasPrecision(18, 3);

            modelBuilder.Entity<Skill>()
                .Property(s => s.Id)
                .UseIdentityColumn(0, 1);

            modelBuilder.Entity<Skill>()
                .Property(s => s.BaseDamage)
                .HasPrecision(18, 3);

            modelBuilder.Entity<Skill>()
                .Property(s => s.Name)
                .HasMaxLength(50);

            modelBuilder.Entity<Skill>()
                .Property(s => s.IconPath)
                .HasMaxLength(50);

            modelBuilder.Entity<SkillDamageMultiplier>()
                .HasKey(sdm => new { sdm.SkillId, sdm.AttributeId });

            modelBuilder.Entity<SkillDamageMultiplier>()
                .Property(sdm => sdm.Multiplier)
                .HasPrecision(18, 3);

            modelBuilder.Entity<ItemModSlotType>()
                .Property(st => st.Id)
                .ValueGeneratedNever();

            modelBuilder.Entity<ItemModSlotType>()
                .HasEnumValues<ItemModSlotType, EItemModSlotType>();

            modelBuilder.Entity<ItemModSlotType>()
                .Property(st => st.Name)
                .HasMaxLength(50);

            modelBuilder.Entity<Tag>()
                .Property(t => t.Name)
                .HasMaxLength(50);

            modelBuilder.Entity<TagCategory>()
                .Property(tc => tc.Id)
                .ValueGeneratedNever();

            modelBuilder.Entity<TagCategory>()
                .HasEnumValues<TagCategory, ETagCategory>();

            modelBuilder.Entity<TagCategory>()
                .Property(tc => tc.Name)
                .HasMaxLength(50);

            modelBuilder.Entity<Zone>()
                .Property(z => z.Id)
                .UseIdentityColumn(0, 1);

            modelBuilder.Entity<Zone>()
                .Property(z => z.Name)
                .HasMaxLength(50);

            modelBuilder.Entity<ZoneDrop>()
                .Property(zd => zd.DropRate)
                .HasPrecision(9, 8);

            modelBuilder.Entity<ZoneEnemy>()
                .ToTable(tb => tb.HasTrigger("trig_ZoneEnemies_ProbabilityRecalc"));

            modelBuilder.Entity<ZoneEnemyAlias>()
                .HasKey(zea => zea.ZoneEnemyId);

            modelBuilder.Entity<ZoneEnemyProbability>()
                .HasKey(zep => zep.ZoneEnemyId);

            modelBuilder.Entity<ZoneEnemyProbability>()
                .Property(zep => zep.Probability)
                .HasPrecision(18, 3);
        }
    }
}
