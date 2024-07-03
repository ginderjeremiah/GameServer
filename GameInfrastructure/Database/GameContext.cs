using GameCore;
using GameCore.Entities;
using Microsoft.EntityFrameworkCore;
using Attribute = GameCore.Entities.Attribute;

namespace GameInfrastructure.Database
{
    public class GameContext : DbContext
    {
        public GameContext() { }

        public GameContext(DbContextOptions<GameContext> options) : base(options) { }

        public DbSet<AttributeDistribution> AttributeDistributions { get; set; }
        public DbSet<Attribute> Attributes { get; set; }
        public DbSet<Enemy> Enemies { get; set; }
        public DbSet<EnemyDrop> EnemyDrops { get; set; }
        public DbSet<EnemySkill> EnemySkills { get; set; }
        public DbSet<EquipmentSlot> EquipmentSlots { get; set; }
        public DbSet<InventoryItemMod> InventoryItemMods { get; set; }
        public DbSet<InventoryItem> InventoryItems { get; set; }
        public DbSet<ItemAttribute> ItemAttributes { get; set; }
        public DbSet<ItemCategory> ItemCategories { get; set; }
        public DbSet<ItemModAttribute> ItemModAttributes { get; set; }
        public DbSet<ItemMod> ItemMods { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<ItemSlot> ItemSlots { get; set; }
        public DbSet<LogPreference> LogPreferences { get; set; }
        public DbSet<LogSetting> LogSettings { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<PlayerAttribute> PlayerAttributes { get; set; }
        public DbSet<PlayerSkill> PlayerSkills { get; set; }
        public DbSet<Skill> Skills { get; set; }
        public DbSet<SkillDamageMultiplier> SkillDamageMultipliers { get; set; }
        public DbSet<SlotType> SlotTypes { get; set; }
        public DbSet<TagCategory> TagCategories { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<ZoneEnemy> ZoneEnemies { get; set; }
        public DbSet<ZoneEnemyAlias> ZoneEnemyAliases { get; set; }
        public DbSet<ZoneEnemyProbability> ZoneEnemyProbabilities { get; set; }
        public DbSet<Zone> Zones { get; set; }
        public DbSet<ZoneDrop> ZoneDrops { get; set; }

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
                .HasOne(iim => iim.ItemSlot)
                .WithMany(isl => isl.InventoryItemMods)
                .OnDelete(DeleteBehavior.NoAction);

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

            modelBuilder.Entity<ItemSlot>()
                .Property(isl => isl.Probability)
                .HasPrecision(9, 8);

            modelBuilder.Entity<ItemSlot>()
                .Property(isl => isl.GuaranteedItemModId)
                .IsRequired(false);

            //Fix to prevent "possible" circular constraint when SlotType is deleted.
            modelBuilder.Entity<ItemSlot>()
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

            modelBuilder.Entity<SlotType>()
                .Property(st => st.Id)
                .ValueGeneratedNever();

            modelBuilder.Entity<SlotType>()
                .HasEnumValues<SlotType, ESlotType>();

            modelBuilder.Entity<SlotType>()
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
