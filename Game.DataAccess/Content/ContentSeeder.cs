using Game.Abstractions.Content;
using Game.DataAccess.Mapping;
using Game.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using EntityItemTag = Game.Infrastructure.Entities.ItemTag;
using EntityItemModTag = Game.Infrastructure.Entities.ItemModTag;
using EntitySkill = Game.Infrastructure.Entities.Skill;

namespace Game.DataAccess.Content
{
    /// <inheritdoc cref="IContentSeeder"/>
    internal sealed class ContentSeeder : IContentSeeder
    {
        // Arbitrary key for the seed step's session-scoped Postgres advisory lock — the only one this app
        // takes directly, distinct from EF's own migration-history advisory lock key (which is released
        // before seeding runs anyway).
        private const long SeedAdvisoryLockKey = 2042;

        private readonly GameContext _context;

        public ContentSeeder(GameContext context)
        {
            _context = context;
        }

        public async Task<bool> SeedAsync(ContentImport content, CancellationToken cancellationToken = default)
        {
            // Skills are the foundational set (the punch fallback, class kit, enemy/proficiency skills all
            // reference them), so their presence marks an already-populated database. Fresh DB only: never
            // overwrite an authored dev database or double-seed.
            if (await _context.Set<EntitySkill>().AnyAsync(cancellationToken))
            {
                return false;
            }

            // Map every set to its entity graph up front; the parents carry their child rows so each set is
            // inserted then flattened, and the enemy graph carries its spawn-table joins for a later step.
            var skills = content.Skills.Select(SkillMapper.ToEntity).ToList();
            var tags = content.Tags.Select(TagMapper.ToEntity).ToList();
            var itemMods = content.ItemMods.Select(ItemMapper.ModToEntity).ToList();
            var items = content.Items.Select(ItemMapper.ToEntity).ToList();
            var enemies = content.Enemies.Select(EnemyMapper.ToEntity).ToList();
            var challenges = content.Challenges.Select(ChallengeMapper.ToEntity).ToList();
            var zones = content.Zones.Select(ZoneMapper.ToEntity).ToList();
            var classes = content.Classes.Select(ClassMapper.ToEntity).ToList();
            var paths = content.Paths.Select(PathMapper.ToEntity).ToList();
            var proficiencies = content.Proficiencies.Select(ProficiencyMapper.ToEntity).ToList();
            var recipes = content.SkillRecipes.Select(SkillRecipeMapper.ToEntity).ToList();
            var lessons = content.Lessons.Select(LessonMapper.ToEntity).ToList();

            // Tag assignments are join rows over the Tag catalogue (seeded below, ahead of the join rows) rather
            // than child entities of the item/mod graph, so they are built straight from the contracts here.
            var itemTags = content.Items
                .SelectMany(item => item.Tags.Select(tagId => new EntityItemTag { ItemId = item.Id, TagId = tagId }))
                .ToList();
            var itemModTags = content.ItemMods
                .SelectMany(mod => mod.Tags.Select(tagId => new EntityItemModTag { ItemModId = mod.Id, TagId = tagId }))
                .ToList();

            // Every entity type the inserts touch, so sequence advancement is derived from what was actually
            // seeded rather than a hand-maintained parallel list (AdvanceIdentitySequencesAsync filters the
            // composite-key join tables out).
            var insertedTypes = new HashSet<Type>();

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            // The guard above runs outside the transaction (default READ COMMITTED), so two instances booting
            // concurrently against a fresh database can both pass it. The advisory lock serializes the actual
            // seed: it blocks until any other in-flight seed transaction commits or rolls back, then the
            // recheck below sees that transaction's result — so at most one instance ever inserts, and a
            // loser skips cleanly instead of racing the insert into a PK violation. Xact-scoped, so it
            // releases automatically on commit/rollback; mirrors the row-lock check-then-act pattern in
            // Users.cs, just with no natural row to lock here.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({SeedAdvisoryLockKey})", cancellationToken);
            if (await _context.Set<EntitySkill>().AnyAsync(cancellationToken))
            {
                return false;
            }

            // Insert referenced sets before referencing ones, and parents before their children. Paths and
            // skills have no static-content dependencies; proficiencies reward skills; items gate on
            // proficiencies and grant skills; challenges reward items/mods; zones point at boss enemies and
            // unlock challenges; the enemy spawn table needs both zones and enemies; classes reference skills
            // and items; recipes reference skills and proficiencies.
            await Insert(paths);

            await Insert(skills);
            await Insert(skills.SelectMany(s => s.SkillDamagePortions).ToList());
            await Insert(skills.SelectMany(s => s.SkillDamageMultipliers).ToList());
            await Insert(skills.SelectMany(s => s.SkillEffects).ToList());

            await Insert(proficiencies);
            await Insert(proficiencies.SelectMany(p => p.LevelModifiers).ToList());
            await Insert(proficiencies.SelectMany(p => p.LevelRewards).ToList());
            await Insert(proficiencies.SelectMany(p => p.Prerequisites).ToList());

            // Tags carry their own (non-zero-based) identity and are referenced by the item/mod tag-join rows,
            // so the Tag catalogue is seeded before those rows land. The referenced TagCategory is intrinsic
            // (migration-seeded), so it already exists on a fresh database.
            await Insert(tags);

            await Insert(itemMods);
            await Insert(itemMods.SelectMany(m => m.ItemModAttributes).ToList());
            await Insert(itemModTags);

            await Insert(items);
            await Insert(items.SelectMany(i => i.ItemAttributes).ToList());
            await Insert(items.SelectMany(i => i.ItemModSlots).ToList());
            await Insert(itemTags);

            await Insert(enemies);
            await Insert(enemies.SelectMany(e => e.AttributeDistributions).ToList());
            await Insert(enemies.SelectMany(e => e.EnemySkills).ToList());

            await Insert(challenges);

            await Insert(zones);

            // Spawn-table joins reference both a zone and an enemy, so they land after both sets exist.
            await Insert(enemies.SelectMany(e => e.ZoneEnemies).ToList());

            await Insert(classes);
            await Insert(classes.SelectMany(c => c.StarterSkills).ToList());
            await Insert(classes.SelectMany(c => c.StarterEquipment).ToList());
            await Insert(classes.SelectMany(c => c.AttributeDistributions).ToList());

            await Insert(recipes);
            await Insert(recipes.SelectMany(r => r.Inputs).ToList());
            await Insert(recipes.SelectMany(r => r.Conditions).ToList());

            // Lessons have no static-content dependencies of their own.
            await Insert(lessons);
            await Insert(lessons.SelectMany(l => l.Steps).ToList());

            // Advance every seeded identity table's sequence past its highest explicit id (the guard filters
            // the composite-key join tables out, so passing every inserted type is safe and stays in sync).
            await EntityRowInserter.AdvanceIdentitySequencesAsync(_context, insertedTypes, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return true;

            Task Insert<T>(IReadOnlyList<T> rows)
            {
                insertedTypes.Add(typeof(T));
                return EntityRowInserter.InsertAsync(_context, rows, cancellationToken);
            }
        }
    }
}
