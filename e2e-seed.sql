-- Baseline static reference data for the end-to-end (Playwright) stack.
--
-- WHY THIS EXISTS: the dockerized Game.Api applies EF migrations on startup, but those migrations
-- only seed the *intrinsic*, enum-backed reference data (Attributes, Roles, LogTypes, …) via
-- HasData. The *static* reference data (Skills, Enemies, Zones, …) is content authored through the
-- admin tools and is deliberately NOT encoded in the application or its migrations (see
-- docs/backend.md "Reference Data"). A freshly-migrated database therefore has none of it.
--
-- Character creation, however, gives every new player the class-kit starter skills 1/2/3 (built by
-- NewPlayerFactory, persisted by LoginController.CreatePlayer), and the loadout gate fields the punch
-- fallback (skill 0). Signup itself creates no character now (#1256) — the first one is created on the
-- select screen — but the suite's signup flow then creates that first character there, so without at least
-- those Skill rows it fails with an FK violation (FK_PlayerSkills_Skills_SkillId) and the whole e2e suite
-- cannot reach the game. This script seeds the minimal, coherent slice of static reference data the suite needs:
--   * Skills 0-3            – the punch fallback (0) + the class kit (1/2/3), required by new-character creation
--   * two combat zones      – a starter zone and a gated second zone, each with a spawn table, so both are
--                             playable once reached and the Enemies admin catalogue has rows
--   * a dedicated zone boss – so the "Challenge Boss" flow can be exercised end-to-end (#220)
--
-- Rows are inserted in dependency order (Enemies before the Zones that reference them as bosses).
-- It is applied against the e2e Postgres AFTER the API has migrated (the CI workflow runs it once
-- the API answers), and the workflow then restarts the API so its startup-loaded reference-data
-- caches (#357) reflect the seed. Every statement is idempotent (ON CONFLICT DO NOTHING), so
-- re-running is safe. It is intentionally separate from the app: production/local databases are
-- never touched by it.

-- Starter skills. Skill 0 is the virtual-fists "punch" (GameConstants.PunchSkillId, #1342): the bare-hands
-- signature the weapon-match loadout gate fields when no weapon is equipped — the foundational, always-available
-- skill, so it takes id 0. It is NOT in the class kit (not selectable/granted-by-item); it comes online only
-- through the gate's empty-weapon-slot path. Skills 1/2/3 are the class kit granted on character creation
-- (NewPlayerFactory, persisted by LoginController.CreatePlayer).
--
-- Acquisition (ESkillAcquisition bitmask) is authoring intent, and it must match how each skill is actually
-- granted or the progression-graph lint (#1420) warns. Strike (1) and Cleave (2) are both class-kit skills
-- AND the seeded enemies' pool skills, so they carry Player|Enemy (1|4 = 5); the enemy skill-set save rejects
-- any pool skill that isn't Enemy-flagged, so this is exactly what the Workbench would author. Punch (0) and
-- Focus (3) stay Player-only (1) — no enemy uses them. (The column defaults to Player=1, but we set it
-- explicitly here so the dual flags are visible in the seed and survive the content export.)
INSERT INTO "Skills" ("Id", "Name", "BaseDamage", "Description", "CooldownMs", "IconPath", "Acquisition") VALUES
  (0, 'Punch', 4, 'A bare-handed strike.', 1000, '', 1),
  (1, 'Strike', 10, 'A basic physical attack.', 1000, '', 5),
  (2, 'Cleave', 8, 'A sweeping blow that favours raw power.', 1500, '', 5),
  (3, 'Focus', 6, 'Channel energy into a sharper, magical hit.', 1200, '', 1)
ON CONFLICT ("Id") DO NOTHING;

-- How each skill scales (Strength = 0, Intellect = 2 per EAttribute). Focus is the lone Intellect skill.
INSERT INTO "SkillDamageMultipliers" ("SkillId", "AttributeId", "Multiplier") VALUES
  (0, 0, 1.0),
  (1, 0, 1.0),
  (2, 0, 1.0),
  (3, 2, 1.0)
ON CONFLICT ("SkillId", "AttributeId") DO NOTHING;

-- Each skill's direct-hit damage portions (#1343). Every skill must carry at least one positive-weight portion;
-- the #1384 migration backfills authored skills to a single full-weight Physical portion (DamageType = 0 per
-- EDamageType), but this hand-seeded reference data is inserted directly, so it must declare the rows itself.
-- The direct-hit pipeline (#1385) splits a hit across these portions — a skill with none would split across zero
-- and deal no damage, so the boss fight would never resolve. Punch (0) is a single full-weight Unarmed portion
-- (EDamageType.Unarmed = 13), so the weapon-match gate (#1342) reads it as a weapon-leaf Unarmed skill and fields
-- it only bare-handed (or with an Unarmed weapon); the class-kit skills (1/2/3) are Physical (weapon-agnostic).
INSERT INTO "SkillDamagePortions" ("SkillId", "DamageType", "Weight") VALUES
  (0, 13, 1.0),
  (1, 0, 1.0),
  (2, 0, 1.0),
  (3, 0, 1.0)
ON CONFLICT ("SkillId", "DamageType") DO NOTHING;

-- A starter class (id 0). Character creation requires a class (#1221): the select-screen create form
-- sends a classId, which the API rejects unless it resolves to a live class, so the suite's
-- first-character creation needs class 0 to exist. Its kit is the starter skills 1/2/3 (Strike/Cleave/Focus —
-- the selectable skills new characters receive; the punch fallback, skill 0, is fielded by the gate, not the
-- kit) and a uniform base spread of 5 across the six core attributes (ids 0-5), mirroring the former flat
-- starting allocation so a fresh e2e character is playable. The signature passive is a no-op (amount 0,
-- Additive = 1 per EModifierType).
INSERT INTO "Classes" ("Id", "Name", "Description", "Word", "PassiveAttributeId", "PassiveAmount", "PassiveScalingAttributeId", "PassiveScalingAmount", "PassiveModifierType", "RetiredAt") VALUES
  (0, 'Adventurer', 'A versatile starting archetype.', 'aenkor', 0, 0, NULL, 0, 1, NULL)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "ClassStarterSkills" ("ClassId", "SkillId") VALUES
  (0, 1),
  (0, 2),
  (0, 3)
ON CONFLICT ("ClassId", "SkillId") DO NOTHING;

-- Base spread of 5 across the six core attributes (Strength=0 … Luck=5 per EAttribute), no per-level growth.
INSERT INTO "ClassAttributeDistributions" ("ClassId", "AttributeId", "BaseAmount", "AmountPerLevel") VALUES
  (0, 0, 5, 0),
  (0, 1, 5, 0),
  (0, 2, 5, 0),
  (0, 3, 5, 0),
  (0, 4, 5, 0),
  (0, 5, 5, 0)
ON CONFLICT ("ClassId", "AttributeId") DO NOTHING;

-- A progression-gating challenge: "clear the starter zone" (ZonesCleared = 3 per EChallengeType,
-- targeting zone 0, goal 1). The second zone below is gated behind it, so a brand-new player — who
-- has not cleared anything — sees the forward zone-nav arrow locked. This exercises the locked path
-- end-to-end; the unlock transition itself is covered by the unit/integration suites.
INSERT INTO "Challenges" ("Id", "Name", "Description", "ChallengeTypeId", "TargetEntityId", "ProgressGoal") VALUES
  (0, 'Clear Verdant Hollow', 'Clear the starter zone to press onward.', 3, 0, 1)
ON CONFLICT ("Id") DO NOTHING;

-- A couple of low-level enemies to populate the starter zone (and the Enemies admin catalogue), plus a
-- dedicated zone boss (IsBoss). The boss is NOT part of the random ZoneEnemies spawn table — it is
-- fought only via the zone's BossEnemyId (set below) through the deterministic "Challenge Boss"
-- action. Enemies 3/4 populate the gated second zone (Ashen Wastes) so it is a real combat zone rather
-- than an empty one a player would be relocated out of. Inserted before the Zones so the
-- FK_Zones_Enemies_BossEnemyId reference resolves.
INSERT INTO "Enemies" ("Id", "Name", "IsBoss") VALUES
  (0, 'Forest Slime', false),
  (1, 'Wild Boar', false),
  (2, 'Direboar Alpha', true),
  (3, 'Cinder Hound', false),
  (4, 'Ashen Revenant', false)
ON CONFLICT ("Id") DO NOTHING;

-- Enemy attribute distributions (Strength = 0, Endurance = 1 per EAttribute). The boss (2) is a
-- deliberately glass-cannon bruiser — Strength only — so at its fought level it hits hard but has
-- modest health. That keeps the real-time e2e boss fight short while a fresh level-1 player still
-- wins it deterministically (the boss never out-damages the player's health pool). These numbers
-- are e2e scaffolding tuned for a quick, winnable fight, not gameplay balance.
-- Ashen Wastes enemies (3/4) sit in a higher level band (8-18), so their base/per-level are scaled up from
-- the starter zone's. These are e2e scaffolding numbers (a coherent, populated second zone), not tuned balance.
INSERT INTO "AttributeDistributions" ("EnemyId", "AttributeId", "BaseAmount", "AmountPerLevel") VALUES
  (0, 0, 5, 1.0),
  (0, 1, 5, 1.0),
  (1, 0, 7, 1.5),
  (1, 1, 6, 1.0),
  (2, 0, 1, 1.0),
  (3, 0, 12, 2.0),
  (3, 1, 8, 1.5),
  (4, 0, 10, 2.0),
  (4, 1, 10, 1.5)
ON CONFLICT ("EnemyId", "AttributeId") DO NOTHING;

-- Give the enemies a skill so battles resolve. Strike (1) and Cleave (2) are Enemy-flagged (see the Skills
-- insert), so an enemy pool may hold them. The boss brings its full authored loadout (Strike id 1 + Cleave
-- id 2), which the deterministic "Challenge Boss" path fights it with in full (SelectAllBattleSkills); the
-- Ashen Wastes enemies (3/4) each swing Strike.
INSERT INTO "EnemySkills" ("EnemyId", "SkillId") VALUES
  (0, 1),
  (1, 1),
  (2, 1),
  (2, 2),
  (3, 1),
  (4, 1)
ON CONFLICT ("EnemyId", "SkillId") DO NOTHING;

-- The starter zone (Player.CurrentZoneId defaults to 0) is always open and hosts a dedicated boss
-- (enemy 2, fought at the zone's top level via BossLevel), so a brand-new player sees the fight
-- screen's "Challenge Boss" affordance light up. The next zone is gated behind the challenge above
-- (UnlockChallengeId = 0) and has no boss (BossLevel = 1 is the column's NOT NULL default and is
-- meaningless without a BossEnemyId), but it does carry its own spawn table (enemies 3/4) so it is a
-- real combat zone once unlocked rather than an empty one the player is immediately relocated out of.
--
-- The Home zone (IsHome = true) is a no-combat sanctuary: no enemies spawn there and never will (it
-- carries no boss and no ZoneEnemies rows). Order = -1 places it leftmost in the zone nav. The backend
-- refuses a battle zone-change into it, so it never becomes a player's CurrentZoneId — offline rewards
-- keep crediting their last real combat zone.
INSERT INTO "Zones" ("Id", "Name", "Description", "Order", "LevelMin", "LevelMax", "BossEnemyId", "BossLevel", "UnlockChallengeId", "IsHome") VALUES
  (0, 'Verdant Hollow', 'A quiet glade where new heroes cut their teeth.', 0, 1, 10, 2, 10, NULL, false),
  (1, 'Ashen Wastes', 'A scorched expanse — sealed until Verdant Hollow is cleared.', 1, 8, 18, NULL, 1, 0, false),
  (2, 'Home', 'A quiet refuge where you can rest without battling. No enemies will find you here.', -1, 1, 1, NULL, 1, NULL, true)
ON CONFLICT ("Id") DO NOTHING;

-- Place the (non-boss) enemies in their zones (equal spawn weight per zone): Forest Slime/Wild Boar in the
-- starter zone, Cinder Hound/Ashen Revenant in Ashen Wastes. The boss is excluded — it is challenged
-- explicitly, not rolled into the random idle encounter table.
INSERT INTO "ZoneEnemies" ("ZoneId", "EnemyId", "Weight") VALUES
  (0, 0, 1),
  (0, 1, 1),
  (1, 3, 1),
  (1, 4, 1)
ON CONFLICT ("ZoneId", "EnemyId") DO NOTHING;

-- The base tables use GENERATED BY DEFAULT identity columns seeded above with explicit ids, which
-- leaves their sequences untouched. Advance each past the highest seeded id so later DB-generated
-- inserts (e.g. creating a record through the admin tools) don't collide with a seeded row.
SELECT setval(pg_get_serial_sequence('"Skills"', 'Id'), (SELECT MAX("Id") FROM "Skills"));
SELECT setval(pg_get_serial_sequence('"Classes"', 'Id'), (SELECT MAX("Id") FROM "Classes"));
SELECT setval(pg_get_serial_sequence('"Enemies"', 'Id'), (SELECT MAX("Id") FROM "Enemies"));
SELECT setval(pg_get_serial_sequence('"Zones"', 'Id'), (SELECT MAX("Id") FROM "Zones"));
SELECT setval(pg_get_serial_sequence('"Challenges"', 'Id'), (SELECT MAX("Id") FROM "Challenges"));

-- e2e admin provisioning.
--
-- The admin area is role-gated on both ends: the frontend only shows the Admin nav entry / allows
-- the /admin route to users holding the Admin role, and the backend requires that role on every
-- admin endpoint. The e2e suite creates fresh accounts through the real signup flow, which builds
-- the full user+player graph but grants no roles. Rather than hand-seed an admin user (whose
-- password hash and entire player graph would have to be kept in sync with the app), this trigger
-- auto-attaches the seeded Admin role to any account whose username marks it as an admin fixture
-- ('e2eadmin…'). The admin-area tests can then sign in as a genuine admin while still using a unique
-- account per test, keeping them parallel-safe. The role is resolved by name (not a hardcoded id),
-- so it stays correct if the enum is ever renumbered and simply no-ops if the role is absent.
--
-- e2e-only: this lives solely in this seed script, which never touches production/local databases.
-- Idempotent: CREATE OR REPLACE + DROP ... IF EXISTS make re-running safe.
CREATE OR REPLACE FUNCTION e2e_grant_admin_role() RETURNS TRIGGER AS $$
BEGIN
  IF NEW."Username" LIKE 'e2eadmin%' THEN
    INSERT INTO "UserRoles" ("RolesId", "UsersId")
    SELECT "Id", NEW."Id" FROM "Roles" WHERE "Name" = 'Admin'
    ON CONFLICT DO NOTHING;
  END IF;
  RETURN NULL;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS e2e_grant_admin_role_trg ON "Users";
CREATE TRIGGER e2e_grant_admin_role_trg
  AFTER INSERT ON "Users"
  FOR EACH ROW EXECUTE FUNCTION e2e_grant_admin_role();
