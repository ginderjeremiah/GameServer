using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Contracts = Game.Abstractions.Contracts;

namespace Game.Application.Content
{
    /// <summary>
    /// Serializes the static reference-data read contracts to the canonical, source-controlled JSON the
    /// content export commits (spike #1390, decision 2). The format is tuned for review-friendly, byte-stable
    /// diffs: top-level arrays are id-ordered, child collections are emitted in a declared stable order,
    /// enums render as their names, <see cref="DateTime"/>s normalize to UTC ISO-8601, and the indentation
    /// and newline are fixed so the bytes never churn across platforms. Re-serializing the same data is
    /// byte-identical, which is what the CI drift guard relies on.
    /// </summary>
    internal static class ContentExportSerializer
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            IndentCharacter = ' ',
            IndentSize = 2,
            // Pin the newline so committed files are LF on every platform (Windows dev / Linux CI).
            NewLine = "\n",
            // Emit non-ASCII content (em-dashes, accents in authored prose) verbatim rather than \uXXXX
            // escapes so the committed files read naturally; deterministic, and these files are never HTML.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            // Enum names (not numbers) keep the export readable and resilient to enum renumbering; a
            // DateTime renders as a stable UTC ISO-8601 instant rather than a Kind-dependent local string.
            Converters =
            {
                new JsonStringEnumConverter(),
                new UtcIsoDateTimeConverter(),
            },
        };

        /// <summary>Serializes an already-canonically-ordered set to its export JSON (trailing newline included).</summary>
        public static string Serialize<T>(IReadOnlyList<T> orderedItems)
        {
            // The newline is already pinned to "\n" via the options, but normalize defensively so a future
            // option change can never reintroduce CRLF into a committed file; append a trailing newline (POSIX).
            var json = JsonSerializer.Serialize(orderedItems, _options).Replace("\r\n", "\n");
            return json + "\n";
        }

        /// <summary>Deserializes an exported set back into its read contracts, through the same canonical options
        /// (enum names, UTC ISO-8601 instants) the export was written with — the inverse of <see cref="Serialize"/>.
        /// Used by the progression-graph lint (#1420) to load the committed <c>content/*.json</c> snapshot and by
        /// the content seeder (#1419) to reconstruct a fresh database from it.</summary>
        public static IReadOnlyList<T> Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<List<T>>(json, _options)
                ?? throw new InvalidOperationException($"Content export for '{typeof(T).Name}' deserialized to null.");
        }

        // --- Per-set canonical ordering -------------------------------------------------------------------
        // Top-level lists are ordered by id (== cache index); child collections get a declared, total order so
        // the bytes are stable regardless of the order the cache happened to materialize them in. The contracts
        // are freshly mapped per read, so reordering their collections in place is safe.

        public static IReadOnlyList<Contracts.Skill> Canonicalize(IEnumerable<Contracts.Skill> skills)
        {
            var ordered = skills.OrderBy(s => s.Id).ToList();
            foreach (var skill in ordered)
            {
                skill.DamageMultipliers = skill.DamageMultipliers.OrderBy(m => m.AttributeId).ToList();
                skill.DamagePortions = skill.DamagePortions.OrderBy(p => p.Type).ToList();
                skill.Effects = skill.Effects.OrderBy(e => e.Id).ToList();
            }
            return ordered;
        }

        public static IReadOnlyList<Contracts.Tag> Canonicalize(IEnumerable<Contracts.Tag> tags)
        {
            // Tags carry their own non-zero-based identity (resolved by lookup, not by index), but id order is
            // still a total, stable order for the committed file.
            return tags.OrderBy(t => t.Id).ToList();
        }

        public static IReadOnlyList<Contracts.Item> Canonicalize(IEnumerable<Contracts.Item> items)
        {
            var ordered = items.OrderBy(i => i.Id).ToList();
            foreach (var item in ordered)
            {
                item.Attributes = item.Attributes.OrderBy(a => a.AttributeId).ToList();
                item.ModSlots = item.ModSlots.OrderBy(s => s.Id).ToList();
                item.Tags = item.Tags.OrderBy(t => t).ToList();
            }
            return ordered;
        }

        public static IReadOnlyList<Contracts.ItemMod> Canonicalize(IEnumerable<Contracts.ItemMod> mods)
        {
            var ordered = mods.OrderBy(m => m.Id).ToList();
            foreach (var mod in ordered)
            {
                mod.Attributes = mod.Attributes.OrderBy(a => a.AttributeId).ToList();
                mod.Tags = mod.Tags.OrderBy(t => t).ToList();
            }
            return ordered;
        }

        public static IReadOnlyList<Contracts.Enemy> Canonicalize(IEnumerable<Contracts.Enemy> enemies)
        {
            var ordered = enemies.OrderBy(e => e.Id).ToList();
            foreach (var enemy in ordered)
            {
                enemy.AttributeDistribution = enemy.AttributeDistribution.OrderBy(a => a.AttributeId).ToList();
                enemy.SkillPool = enemy.SkillPool.OrderBy(s => s).ToList();
                // ZoneId is unique per spawn today; ThenBy(Weight) makes the order provably total regardless.
                enemy.Spawns = enemy.Spawns.OrderBy(s => s.ZoneId).ThenBy(s => s.Weight).ToList();
            }
            return ordered;
        }

        public static IReadOnlyList<Contracts.Zone> Canonicalize(IEnumerable<Contracts.Zone> zones)
        {
            return zones.OrderBy(z => z.Id).ToList();
        }

        public static IReadOnlyList<Contracts.Challenge> Canonicalize(IEnumerable<Contracts.Challenge> challenges)
        {
            return challenges.OrderBy(c => c.Id).ToList();
        }

        public static IReadOnlyList<Contracts.Class> Canonicalize(IEnumerable<Contracts.Class> classes)
        {
            var ordered = classes.OrderBy(c => c.Id).ToList();
            foreach (var cls in ordered)
            {
                cls.StarterSkillIds = cls.StarterSkillIds.OrderBy(s => s).ToList();
                // EquipmentSlot is unique per starter item today; ThenBy(ItemId) makes the order provably total.
                cls.StarterEquipment = cls.StarterEquipment.OrderBy(e => e.EquipmentSlot).ThenBy(e => e.ItemId).ToList();
                cls.AttributeDistributions = cls.AttributeDistributions.OrderBy(a => a.AttributeId).ToList();
            }
            return ordered;
        }

        public static IReadOnlyList<Contracts.Path> Canonicalize(IEnumerable<Contracts.Path> paths)
        {
            return paths.OrderBy(p => p.Id).ToList();
        }

        public static IReadOnlyList<Contracts.Proficiency> Canonicalize(IEnumerable<Contracts.Proficiency> proficiencies)
        {
            var ordered = proficiencies.OrderBy(p => p.Id).ToList();
            foreach (var prof in ordered)
            {
                prof.LevelModifiers = prof.LevelModifiers
                    .OrderBy(m => m.Level).ThenBy(m => m.AttributeId).ThenBy(m => m.ModifierTypeId).ToList();
                prof.LevelRewards = prof.LevelRewards
                    .OrderBy(r => r.Level).ThenBy(r => r.RewardSkillId).ToList();
                prof.PrerequisiteIds = prof.PrerequisiteIds.OrderBy(id => id).ToList();
            }
            return ordered;
        }

        public static IReadOnlyList<Contracts.SkillRecipe> Canonicalize(IEnumerable<Contracts.SkillRecipe> recipes)
        {
            var ordered = recipes.OrderBy(r => r.Id).ToList();
            foreach (var recipe in ordered)
            {
                recipe.InputSkillIds = recipe.InputSkillIds.OrderBy(id => id).ToList();
                recipe.Conditions = recipe.Conditions.OrderBy(c => c.ProficiencyId).ThenBy(c => c.MinLevel).ToList();
            }
            return ordered;
        }

        public static IReadOnlyList<Contracts.Lesson> Canonicalize(IEnumerable<Contracts.Lesson> lessons)
        {
            var ordered = lessons.OrderBy(l => l.Id).ToList();
            foreach (var lesson in ordered)
            {
                lesson.Steps = lesson.Steps.OrderBy(s => s.Ordinal).ToList();
            }
            return ordered;
        }

        /// <summary>
        /// Writes a <see cref="DateTime"/> as a stable UTC ISO-8601 instant so a retire timestamp's serialized
        /// form never depends on the reader's locale or the value's <see cref="DateTimeKind"/>. An Unspecified
        /// kind (the shape a bare <c>timestamp</c> column reads back as) is taken to already be UTC.
        /// </summary>
        internal sealed class UtcIsoDateTimeConverter : JsonConverter<DateTime>
        {
            private const string Format = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

            // Read is the inverse of the canonical Write — the export only writes, but a round-trip parse keeps
            // the converter symmetric and is what the JSON-driven seeder (#1419) will deserialize through.
            public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var value = reader.GetString();
                return DateTime.Parse(value ?? string.Empty, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            }

            public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            {
                var utc = value.Kind switch
                {
                    DateTimeKind.Utc => value,
                    DateTimeKind.Local => value.ToUniversalTime(),
                    _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
                };
                writer.WriteStringValue(utc.ToString(Format, CultureInfo.InvariantCulture));
            }
        }
    }
}
