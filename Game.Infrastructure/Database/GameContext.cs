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
        public DbSet<ItemModType> ItemModTypes { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<TagCategory> TagCategories { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<Tag> Tags { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<ZoneEnemy> ZoneEnemies { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<Zone> Zones { get; set; }
        /// <inheritdoc cref="DbSet{TEntity}"/>
        public DbSet<ZoneDrop> ZoneDrops { get; set; }

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Attribute>(entity =>
            {
                entity.Property(a => a.Id)
                    .ValueGeneratedNever();

                entity.Property(a => a.Name)
                    .HasMaxLength(50);

                entity.HasEnumValues<Attribute, EAttribute>();
            });

            modelBuilder.Entity<AttributeDistribution>(entity =>
            {
                entity.HasKey(ad => new { ad.EnemyId, ad.AttributeId });

                entity.Property(ad => ad.AmountPerLevel)
                    .HasPrecision(18, 3);

                entity.Property(ad => ad.BaseAmount)
                    .HasPrecision(18, 3);
            });

            modelBuilder.Entity<Enemy>(entity =>
            {
                entity.Property(e => e.Id)
                    .UseIdentityColumn(0, 1) //SQL Server
                    .HasIdentityOptions(0, 1, 0); //PostgreSQL

                entity.Property(e => e.Name)
                    .HasMaxLength(50);
            });

            modelBuilder.Entity<EnemyDrop>()
                .Property(ed => ed.DropRate)
                .HasPrecision(9, 8);

            modelBuilder.Entity<EnemySkill>()
                .HasKey(es => new { es.EnemyId, es.SkillId });

            modelBuilder.Entity<EquipmentSlot>(entity =>
            {
                entity.Property(es => es.Id)
                    .ValueGeneratedNever();

                entity.Property(es => es.Name)
                    .HasMaxLength(50);

                entity.HasEnumValues<EquipmentSlot, EEquipmentSlot>();
            });

            modelBuilder.Entity<InventoryItemMod>(entity =>
            {
                entity.HasKey(iim => new { iim.InventoryItemId, iim.ItemModId });

                //Fix to prevent double cascading delete on SlotType => ItemSlot/ItemMod
                entity.HasOne(iim => iim.ItemModSlot)
                    .WithMany(isl => isl.InventoryItemMods)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            //Do not enforce this constraint as it makes it difficult to modify data.
            //modelBuilder.Entity<InventoryItem>()
            //    .HasIndex(ii => new { ii.PlayerId, ii.Equipped, ii.InventorySlotNumber })
            //    .IsUnique();

            modelBuilder.Entity<Item>(entity =>
            {
                entity.Property(i => i.Id)
                    .UseIdentityColumn(0, 1) //SQL Server
                    .HasIdentityOptions(0, 1, 0); //PostgreSQL

                entity.Property(i => i.Name)
                    .HasMaxLength(50);

                entity.Property(i => i.IconPath)
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

                entity.HasEnumValues<ItemCategory, EItemCategory>();
            });

            modelBuilder.Entity<ItemMod>(entity =>
            {
                entity.Property(im => im.Id)
                    .UseIdentityColumn(0, 1) //SQL Server
                    .HasIdentityOptions(0, 1, 0); //PostgreSQL

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

            modelBuilder.Entity<ItemModSlot>(entity =>
            {
                entity.Property(isl => isl.Probability)
                    .HasPrecision(9, 8);

                entity.Property(isl => isl.GuaranteedItemModId)
                    .IsRequired(false);

                //Fix to prevent "possible" circular constraint when SlotType is deleted.
                entity.HasOne(isl => isl.GuaranteedItemMod)
                .WithMany(im => im.GuaranteedSlots)
                .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<LogPreference>()
                .HasKey(lp => new { lp.PlayerId, lp.LogSettingId });

            modelBuilder.Entity<LogSetting>(entity =>
            {
                entity.Property(ls => ls.Id)
                    .ValueGeneratedNever();

                entity.Property(ls => ls.Name)
                    .HasMaxLength(20);

                entity.HasEnumValues<LogSetting, ELogSetting>();
            });

            modelBuilder.Entity<Player>(entity =>
            {
                entity.Property(p => p.UserName)
                    .HasMaxLength(20);

                entity.Property(p => p.Name)
                    .HasMaxLength(20);

                entity.Property(p => p.PassHash)
                    .HasMaxLength(88);
            });

            modelBuilder.Entity<PlayerSkill>()
                .HasKey(ps => new { ps.PlayerId, ps.SkillId });

            modelBuilder.Entity<PlayerAttribute>(entity =>
            {
                entity.HasKey(pa => new { pa.PlayerId, pa.AttributeId });

                entity.Property(pa => pa.Amount)
                    .HasPrecision(18, 3);
            });

            modelBuilder.Entity<Skill>(entity =>
            {
                entity.Property(s => s.Id)
                    .UseIdentityColumn(0, 1) //SQL Server
                    .HasIdentityOptions(0, 1, 0); //PostgreSQL

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

            modelBuilder.Entity<ItemModType>(entity =>
            {
                entity.Property(st => st.Id)
                    .ValueGeneratedNever();

                entity.Property(st => st.Name)
                    .HasMaxLength(50);

                entity.HasEnumValues<ItemModType, EItemModType>();
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

                entity.HasEnumValues<TagCategory, ETagCategory>();
            });

            modelBuilder.Entity<Zone>(entity =>
            {
                entity.Property(z => z.Id)
                    .UseIdentityColumn(0, 1) //SQL Server
                    .HasIdentityOptions(0, 1, 0); //PostgreSQL

                entity.Property(z => z.Name)
                    .HasMaxLength(50);
            });

            modelBuilder.Entity<ZoneDrop>()
                .Property(zd => zd.DropRate)
                .HasPrecision(9, 8);
        }
    }
}
