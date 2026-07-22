using System.Collections.Concurrent;
using Game.Infrastructure.Entities;
using Game.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Attribute = Game.Infrastructure.Entities.Attribute;
using Path = Game.Infrastructure.Entities.Path;

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
        public DbSet<Class> Classes { get; set; }
        public DbSet<ClassStarterSkill> ClassStarterSkills { get; set; }
        public DbSet<ClassStarterEquipment> ClassStarterEquipment { get; set; }
        public DbSet<ClassAttributeDistribution> ClassAttributeDistributions { get; set; }
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
        public DbSet<Lesson> Lessons { get; set; }
        public DbSet<LessonStep> LessonSteps { get; set; }
        public DbSet<LogPreference> LogPreferences { get; set; }
        public DbSet<LogType> LogTypes { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<PlayerAttribute> PlayerAttributes { get; set; }
        public DbSet<PlayerChallenge> PlayerChallenges { get; set; }
        public DbSet<PlayerLesson> PlayerLessons { get; set; }
        public DbSet<PlayerProficiency> PlayerProficiencies { get; set; }
        public DbSet<PlayerSkill> PlayerSkills { get; set; }
        public DbSet<PlayerStatistic> PlayerStatistics { get; set; }
        public DbSet<Rarity> Rarities { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Skill> Skills { get; set; }
        public DbSet<SkillDamagePortion> SkillDamagePortions { get; set; }
        public DbSet<SkillDamageMultiplier> SkillDamageMultipliers { get; set; }
        public DbSet<SkillEffect> SkillEffects { get; set; }
        public DbSet<SkillRecipe> SkillRecipes { get; set; }
        public DbSet<SkillRecipeInput> SkillRecipeInputs { get; set; }
        public DbSet<SkillRecipeCondition> SkillRecipeConditions { get; set; }
        public DbSet<Path> Paths { get; set; }
        public DbSet<Proficiency> Proficiencies { get; set; }
        public DbSet<ProficiencyLevelModifier> ProficiencyLevelModifiers { get; set; }
        public DbSet<ProficiencyLevelReward> ProficiencyLevelRewards { get; set; }
        public DbSet<ProficiencyPrerequisite> ProficiencyPrerequisites { get; set; }
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

                // ItemModSlot is legitimately hard-deletable (AdminItems.SaveModSlots), unlike the retire-only
                // Player/Item/ItemMod it also references. Restrict instead of the convention's Cascade, so a
                // slot delete can never silently destroy every player's applied mod occupying it — the DB-level
                // backstop behind SaveModSlots' own in-use rejection.
                entity.HasOne(am => am.ItemModSlot)
                    .WithMany()
                    .HasForeignKey(am => am.ItemModSlotId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Attribute>(entity =>
            {
                entity.Property(a => a.Id)
                    .ValueGeneratedNever();

                entity.Property(a => a.Name)
                    .HasMaxLength(50);

                entity.Property(a => a.Description)
                    .HasMaxLength(500);

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

                entity.Property(c => c.DesignerNotes)
                    .HasMaxLength(2000);
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

            modelBuilder.Entity<Class>(entity =>
            {
                entity.Property(c => c.Id)
                    .HasIdentityOptions(0, 1, 0);

                entity.Property(c => c.Name)
                    .HasMaxLength(50);

                entity.Property(c => c.Description)
                    .HasMaxLength(500);

                entity.Property(c => c.Word)
                    .HasMaxLength(50);

                entity.Property(c => c.PassiveAmount)
                    .HasPrecision(18, 3);

                entity.Property(c => c.PassiveScalingAmount)
                    .HasPrecision(18, 3);

                entity.Property(c => c.DesignerNotes)
                    .HasMaxLength(2000);
            });

            modelBuilder.Entity<ClassAttributeDistribution>(entity =>
            {
                entity.HasKey(ad => new { ad.ClassId, ad.AttributeId });

                entity.Property(ad => ad.BaseAmount)
                    .HasPrecision(18, 3);

                entity.Property(ad => ad.AmountPerLevel)
                    .HasPrecision(18, 3);

                entity.HasOne(ad => ad.Class)
                    .WithMany(c => c.AttributeDistributions)
                    .HasForeignKey(ad => ad.ClassId);

                entity.HasOne(ad => ad.Attribute)
                    .WithMany()
                    .HasForeignKey(ad => ad.AttributeId);
            });

            modelBuilder.Entity<ClassStarterSkill>(entity =>
            {
                entity.HasKey(s => new { s.ClassId, s.SkillId });

                entity.HasOne(s => s.Class)
                    .WithMany(c => c.StarterSkills)
                    .HasForeignKey(s => s.ClassId);

                entity.HasOne(s => s.Skill)
                    .WithMany()
                    .HasForeignKey(s => s.SkillId);
            });

            modelBuilder.Entity<ClassStarterEquipment>(entity =>
            {
                // One starter item per equipment slot — the slot is the natural key.
                entity.HasKey(e => new { e.ClassId, e.EquipmentSlotId });

                entity.HasOne(e => e.Class)
                    .WithMany(c => c.StarterEquipment)
                    .HasForeignKey(e => e.ClassId);

                entity.HasOne(e => e.Item)
                    .WithMany()
                    .HasForeignKey(e => e.ItemId);
            });

            modelBuilder.Entity<Enemy>(entity =>
            {
                entity.Property(e => e.Id)
                    .HasIdentityOptions(0, 1, 0);

                entity.Property(e => e.Name)
                    .HasMaxLength(50);

                entity.Property(e => e.DesignerNotes)
                    .HasMaxLength(2000);
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

                entity.Property(i => i.Description)
                    .HasMaxLength(500);

                entity.Property(i => i.IconPath)
                    .HasMaxLength(50);

                entity.Property(i => i.DesignerNotes)
                    .HasMaxLength(2000);

                // The proficiency gate is an optional reference to a proficiency. Navigation-less FK (like the
                // zone unlock challenge): deleting the referenced proficiency clears the gate (SetNull),
                // leaving the item ungated, rather than blocking the delete.
                entity.HasOne<Proficiency>()
                    .WithMany()
                    .HasForeignKey(i => i.RequiredProficiencyId)
                    .OnDelete(DeleteBehavior.SetNull);

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

                entity.Property(im => im.Description)
                    .HasMaxLength(500);

                entity.Property(im => im.DesignerNotes)
                    .HasMaxLength(2000);

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

                // The current zone is a required, navigation-less reference. Zones are retire-only (no hard
                // delete), so Restrict never blocks a legitimate operation — it only guards against a bugged
                // write or seed/migration mistake leaving a dangling reference.
                entity.HasOne<Zone>()
                    .WithMany()
                    .HasForeignKey(p => p.CurrentZoneId)
                    .OnDelete(DeleteBehavior.Restrict);

                // The class is a required, navigation-less reference. Classes are retire-only (no hard delete),
                // so Restrict never blocks a legitimate operation.
                entity.HasOne<Class>()
                    .WithMany()
                    .HasForeignKey(p => p.ClassId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PlayerChallenge>(entity =>
            {
                entity.HasKey(pc => new { pc.PlayerId, pc.ChallengeId });

                entity.Property(c => c.Progress)
                    .HasPrecision(36, 3);
            });

            modelBuilder.Entity<PlayerProficiency>(entity =>
            {
                // The composite natural key doubles as the unique constraint the write-behind upsert relies on:
                // a concurrent at-least-once double-apply collides on the PK and is absorbed by the handler's
                // unique-violation-then-retry path (mirroring PlayerChallenge — no separate index needed since
                // neither key column is nullable).
                entity.HasKey(pp => new { pp.PlayerId, pp.ProficiencyId });

                // Xp shares the authored XP-curve precision (Proficiency.BaseXp/XpGrowth) it is compared against.
                entity.Property(pp => pp.Xp)
                    .HasPrecision(18, 3);
            });

            modelBuilder.Entity<PlayerSkill>()
                .HasKey(ps => new { ps.PlayerId, ps.SkillId });

            // The composite natural key doubles as the unique constraint the write-behind upsert relies on,
            // mirroring PlayerProficiency — no separate index needed since neither key column is nullable.
            modelBuilder.Entity<PlayerLesson>()
                .HasKey(pl => new { pl.PlayerId, pl.LessonId });

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

                entity.Property(s => s.CriticalChance)
                    .HasPrecision(18, 3);

                entity.Property(s => s.Name)
                    .HasMaxLength(50);

                entity.Property(s => s.Description)
                    .HasMaxLength(500);

                entity.Property(s => s.IconPath)
                    .HasMaxLength(50);

                entity.Property(s => s.Word)
                    .HasMaxLength(50);

                entity.Property(s => s.Pronunciation)
                    .HasMaxLength(50);

                entity.Property(s => s.Translation)
                    .HasMaxLength(100);

                entity.Property(s => s.DesignerNotes)
                    .HasMaxLength(2000);
            });

            modelBuilder.Entity<SkillDamagePortion>(entity =>
            {
                // Composite key by (skill, type): a skill carries at most one portion per leaf type.
                entity.HasKey(p => new { p.SkillId, p.DamageType });

                entity.Property(p => p.Weight)
                    .HasPrecision(18, 3);
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

            modelBuilder.Entity<SkillRecipe>(entity =>
            {
                entity.Property(r => r.Id)
                    .HasIdentityOptions(0, 1, 0);

                entity.Property(r => r.DesignerNotes)
                    .HasMaxLength(2000);

                // The result skill is retired, never deleted, so Restrict (it is also referenced by the recipe's
                // own input rows). The recipe is itself retired, never deleted.
                entity.HasOne(r => r.ResultSkill)
                    .WithMany()
                    .HasForeignKey(r => r.ResultSkillId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SkillRecipeInput>(entity =>
            {
                entity.HasKey(i => new { i.RecipeId, i.SkillId });

                entity.HasOne(i => i.Recipe)
                    .WithMany(r => r.Inputs)
                    .HasForeignKey(i => i.RecipeId)
                    .OnDelete(DeleteBehavior.Cascade);

                // The input skill is retired, never deleted; Restrict avoids a multiple-cascade-path ambiguity.
                entity.HasOne(i => i.Skill)
                    .WithMany()
                    .HasForeignKey(i => i.SkillId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<SkillRecipeCondition>(entity =>
            {
                entity.HasKey(c => new { c.RecipeId, c.ProficiencyId });

                entity.HasOne(c => c.Recipe)
                    .WithMany(r => r.Conditions)
                    .HasForeignKey(c => c.RecipeId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.Proficiency)
                    .WithMany()
                    .HasForeignKey(c => c.ProficiencyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Path>(entity =>
            {
                entity.Property(p => p.Id)
                    .HasIdentityOptions(0, 1, 0);

                entity.Property(p => p.Name)
                    .HasMaxLength(50);

                entity.Property(p => p.Description)
                    .HasMaxLength(500);

                entity.Property(p => p.DesignerNotes)
                    .HasMaxLength(2000);
            });

            modelBuilder.Entity<Proficiency>(entity =>
            {
                entity.Property(p => p.Id)
                    .HasIdentityOptions(0, 1, 0);

                entity.Property(p => p.Name)
                    .HasMaxLength(50);

                entity.Property(p => p.Description)
                    .HasMaxLength(500);

                entity.Property(p => p.IconPath)
                    .HasMaxLength(50);

                entity.Property(p => p.Word)
                    .HasMaxLength(50);

                entity.Property(p => p.Pronunciation)
                    .HasMaxLength(50);

                entity.Property(p => p.Translation)
                    .HasMaxLength(100);

                entity.Property(p => p.DesignerNotes)
                    .HasMaxLength(2000);

                entity.Property(p => p.BaseXp)
                    .HasPrecision(18, 3);

                entity.Property(p => p.XpGrowth)
                    .HasPrecision(18, 3);

                entity.HasOne(p => p.Path)
                    .WithMany(path => path.Proficiencies)
                    .HasForeignKey(p => p.PathId);

                // A path's tiers occupy distinct ordinals; the unique index bakes that invariant into the
                // schema and makes the home-tier → tier resolution unambiguous.
                entity.HasIndex(p => new { p.PathId, p.PathOrdinal })
                    .IsUnique();
            });

            modelBuilder.Entity<ProficiencyLevelModifier>(entity =>
            {
                entity.HasKey(m => new { m.ProficiencyId, m.Level, m.AttributeId });

                entity.Property(m => m.Amount)
                    .HasPrecision(18, 3);

                entity.HasOne(m => m.Proficiency)
                    .WithMany(p => p.LevelModifiers)
                    .HasForeignKey(m => m.ProficiencyId);

                entity.HasOne(m => m.Attribute)
                    .WithMany()
                    .HasForeignKey(m => m.AttributeId);
            });

            modelBuilder.Entity<ProficiencyLevelReward>(entity =>
            {
                entity.HasKey(r => new { r.ProficiencyId, r.Level });

                entity.HasOne(r => r.Proficiency)
                    .WithMany(p => p.LevelRewards)
                    .HasForeignKey(r => r.ProficiencyId);

                entity.HasOne(r => r.RewardSkill)
                    .WithMany()
                    .HasForeignKey(r => r.RewardSkillId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ProficiencyPrerequisite>(entity =>
            {
                entity.HasKey(p => new { p.ProficiencyId, p.PrerequisiteProficiencyId });

                entity.HasOne(p => p.Proficiency)
                    .WithMany(prof => prof.Prerequisites)
                    .HasForeignKey(p => p.ProficiencyId)
                    .OnDelete(DeleteBehavior.Cascade);

                // The prerequisite side is a second FK to Proficiency; Restrict so the two FKs don't form a
                // multiple-cascade-path ambiguity (a proficiency is retired, never deleted, regardless).
                entity.HasOne(p => p.Prerequisite)
                    .WithMany()
                    .HasForeignKey(p => p.PrerequisiteProficiencyId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Lesson>(entity =>
            {
                entity.Property(l => l.Id)
                    .HasIdentityOptions(0, 1, 0);

                entity.Property(l => l.Key)
                    .HasMaxLength(100);

                entity.Property(l => l.Name)
                    .HasMaxLength(100);

                entity.Property(l => l.ScreenKey)
                    .HasMaxLength(50);

                entity.Property(l => l.DesignerNotes)
                    .HasMaxLength(2000);

                // A lesson's stable authoring slug is how the progression-graph lint matches it against
                // content-design's taught-by-blurb candidates, so two lessons must never share one.
                entity.HasIndex(l => l.Key)
                    .IsUnique();
            });

            modelBuilder.Entity<LessonStep>(entity =>
            {
                entity.HasKey(s => new { s.LessonId, s.Ordinal });

                entity.Property(s => s.Text)
                    .HasMaxLength(500);

                entity.Property(s => s.AnchorKey)
                    .HasMaxLength(100);

                entity.HasOne(s => s.Lesson)
                    .WithMany(l => l.Steps)
                    .HasForeignKey(s => s.LessonId)
                    .OnDelete(DeleteBehavior.Cascade);
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

                // One item per equipment slot, enforced at the DB level so two concurrent equips into the same
                // slot can't both succeed — ItemEquippedHandler's clear-then-write isn't atomic across instances.
                // The partial filter lets the many unequipped (null-slot) rows coexist.
                entity.HasIndex(ui => new { ui.PlayerId, ui.EquipmentSlotId })
                    .IsUnique()
                    .HasFilter("\"EquipmentSlotId\" IS NOT NULL");

                // The equipped slot is an optional, navigation-less reference. Navigation-less FK (like the
                // zone boss/unlock-challenge references): deleting the referenced slot clears the item's
                // equipped state (SetNull) rather than blocking the delete.
                entity.HasOne<EquipmentSlot>()
                    .WithMany()
                    .HasForeignKey(ui => ui.EquipmentSlotId)
                    .OnDelete(DeleteBehavior.SetNull);
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

                entity.Property(z => z.Description)
                    .HasMaxLength(500);

                entity.Property(z => z.DesignerNotes)
                    .HasMaxLength(2000);

                entity.Property(z => z.BossLevel)
                    .HasDefaultValue(1);

                // Backfills existing zones to "not Home" — Home is a deliberately-authored singular sanctuary.
                entity.Property(z => z.IsHome)
                    .HasDefaultValue(false);

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
            ApplyZeroBasedIdentityFixups();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        // Zero-based-identity tables (see IZeroBasedIdentityEntity) seed their Id at 0, so the first row's Id
        // — and any non-nullable int FK referencing it — equals default(int). EF reads that as an unset
        // store-generated value and assigns a temporary key, which would target the wrong row on an UPDATE of
        // record 0 or write a placeholder FK on an INSERT referencing record 0. The fixup forces the literal 0
        // back before the save commits.
        //
        // Which property on a given entity type is such a PK or FK is fixed by the model, so it is derived once
        // per model and cached rather than re-walked from metadata on every save — this runs on the hot
        // write-behind path, where a save touches 1–2 rows per battle tick per connected player.
        private static readonly ConcurrentDictionary<IModel, IReadOnlyDictionary<IEntityType, ZeroBasedFixup>> ZeroBasedFixupsByModel = new();

        internal sealed record ZeroBasedFixup(string? KeyProperty, IReadOnlyList<ForeignKeyFixup> ForeignKeyProperties);

        // A non-nullable int FK targeting a zero-based-identity principal, paired with that principal's entity
        // type so the FK branch below can tell apart the two reasons EF might mark such an FK temporary (#1824):
        // a genuinely unset FK (misread as record 0) versus a real reference to a same-save Added principal
        // whose own id is still pending — see PrincipalEntityType's use in ApplyZeroBasedIdentityFixups.
        internal sealed record ForeignKeyFixup(string PropertyName, IEntityType PrincipalEntityType);

        internal void ApplyZeroBasedIdentityFixups()
        {
            var fixups = ZeroBasedFixupsByModel.GetOrAdd(Model, BuildZeroBasedFixups);
            if (fixups.Count == 0)
            {
                return;
            }

            // Same-save Added zero-based-identity principals still carry a temporary key (their real id isn't
            // assigned until the insert commits). A dependent FK pointing at one of these is legitimately
            // temporary — EF's own relationship fixup propagated that exact temp value — so the FK branch below
            // must leave it alone and let EF resolve it to the principal's real generated id, rather than
            // ForceZero silently repointing it at record 0 (#1824).
            var addedPrincipalTempKeys = new HashSet<(IEntityType EntityType, object TempValue)>();
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Added
                    && fixups.TryGetValue(entry.Metadata, out var principalFixup)
                    && principalFixup.KeyProperty is not null
                    && entry.Property(principalFixup.KeyProperty) is { IsTemporary: true, CurrentValue: { } tempValue })
                {
                    addedPrincipalTempKeys.Add((entry.Metadata, tempValue));
                }
            }

            foreach (var entry in ChangeTracker.Entries())
            {
                if (!fixups.TryGetValue(entry.Metadata, out var fixup))
                {
                    continue;
                }

                // PK branch: a non-Added record whose Id is the seed 0 — ForceZero restores the literal 0 should
                // EF have assigned its key a temporary value (reading 0 as an unset store-generated key). Kept as a
                // DEFENSIVE guard, not because we observe it fire (#1003): editing record 0 the way the admin path
                // does — a fresh entity marked Modified via EntityStore.Update — leaves the key NON-temporary, so
                // ForceZero no-ops. EF Core 10 also forbids the very precondition this branch would correct: a key
                // cannot be temporary while the entry is Modified/Deleted/Unchanged — both setting IsTemporary on
                // such a key and transitioning a temporary-keyed entry out of Added throw, before the save runs. So
                // the branch is currently unreachable and the guard is a no-op either way; it is retained
                // deliberately (against older/future EF semantics or an unforeseen attach path) so a record-0 edit
                // can never silently UPDATE the wrong row. Pinned by AdminEnemiesIntegrationTests
                // .SaveEnemies_EditsRecordZero_UpdatesTheCorrectRow.
                if (fixup.KeyProperty is not null
                    && entry.State is not EntityState.Added
                    && entry.Entity is IZeroBasedIdentityEntity { Id: 0 })
                {
                    ForceZero(entry.Property(fixup.KeyProperty));
                }

                // FK branch: a non-nullable int FK pointing at record 0 of a zero-based principal — unless its
                // temporary value is actually propagated from a same-save Added principal (see
                // addedPrincipalTempKeys above), in which case it is left for EF to resolve to that principal's
                // real generated id instead of being forced to 0.
                foreach (var foreignKey in fixup.ForeignKeyProperties)
                {
                    var property = entry.Property(foreignKey.PropertyName);
                    if (property is { IsTemporary: true, CurrentValue: { } fkTempValue }
                        && addedPrincipalTempKeys.Contains((foreignKey.PrincipalEntityType, fkTempValue)))
                    {
                        continue;
                    }

                    ForceZero(property);
                }
            }
        }

        // Replaces the temporary key EF assigned (treating the seed value 0 as an unset store-generated key)
        // with the literal 0. A no-op when the property was not marked temporary.
        private static void ForceZero(PropertyEntry property)
        {
            if (property.IsTemporary)
            {
                property.IsTemporary = false;
                property.CurrentValue = 0;
            }
        }

        // Derives, per entity type, the zero-based-identity PK and the non-nullable int FKs that reference a
        // zero-based-identity principal. Only entity types with at least one such property are kept, so the
        // per-save loop skips everything else outright.
        internal static IReadOnlyDictionary<IEntityType, ZeroBasedFixup> BuildZeroBasedFixups(IModel model)
        {
            var fixups = new Dictionary<IEntityType, ZeroBasedFixup>();
            foreach (var entityType in model.GetEntityTypes())
            {
                string? keyProperty = null;
                if (entityType.ClrType.IsAssignableTo(typeof(IZeroBasedIdentityEntity)))
                {
                    keyProperty = entityType.FindPrimaryKey()?.Properties
                        .FirstOrDefault(p => p.Name == nameof(IZeroBasedIdentityEntity.Id))?.Name;
                }

                var foreignKeyProperties = new List<ForeignKeyFixup>();
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType != typeof(int) || !property.IsForeignKey())
                    {
                        continue;
                    }

                    var containingForeignKey = property.GetContainingForeignKeys()
                        .FirstOrDefault(fk => fk.DeclaringEntityType == property.DeclaringType);
                    if (containingForeignKey is not null
                        && containingForeignKey.PrincipalEntityType.ClrType.IsAssignableTo(typeof(IZeroBasedIdentityEntity)))
                    {
                        foreignKeyProperties.Add(new ForeignKeyFixup(property.Name, containingForeignKey.PrincipalEntityType));
                    }
                }

                if (keyProperty is not null || foreignKeyProperties.Count > 0)
                {
                    fixups[entityType] = new ZeroBasedFixup(keyProperty, foreignKeyProperties);
                }
            }

            return fixups;
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
