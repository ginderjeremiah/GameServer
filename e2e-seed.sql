-- Baseline static reference data for the end-to-end (Playwright) stack.
--
-- WHY THIS EXISTS: the dockerized Game.Api applies EF migrations on startup, but those migrations
-- only seed the *intrinsic*, enum-backed reference data (Attributes, Roles, LogTypes, …) via
-- HasData. The *static* reference data (Skills, Enemies, Zones, …) is content authored through the
-- admin tools and is deliberately NOT encoded in the application or its migrations (see
-- docs/backend.md "Reference Data"). A freshly-migrated database therefore has none of it.
--
-- Account creation, however, gives every new player the starter skills 0/1/2
-- (LoginController.CreateAccount), so without at least those Skill rows the very first signup fails
-- with an FK violation (FK_PlayerSkills_Skills_SkillId) and the whole e2e suite cannot log in. This
-- script seeds the minimal, coherent slice of static reference data the suite needs:
--   * Skills 0/1/2          – required by new-player creation
--   * one Zone with enemies – so the Enemies admin catalogue has rows and the fight screen is playable
--   * a dedicated zone boss – so the "Challenge Boss" flow can be exercised end-to-end (#220)
--
-- Rows are inserted in dependency order (Enemies before the Zones that reference them as bosses).
-- It is applied against the e2e Postgres AFTER the API has migrated (the CI workflow runs it once
-- the API answers), and the workflow then restarts the API so its startup-loaded reference-data
-- caches (#357) reflect the seed. Every statement is idempotent (ON CONFLICT DO NOTHING), so
-- re-running is safe. It is intentionally separate from the app: production/local databases are
-- never touched by it.

-- Starter skills (ids 0/1/2 are referenced directly by LoginController.CreateAccount).
INSERT INTO "Skills" ("Id", "Name", "BaseDamage", "Description", "CooldownMs", "IconPath") VALUES
  (0, 'Strike', 10, 'A basic physical attack.', 1000, ''),
  (1, 'Cleave', 8, 'A sweeping blow that favours raw power.', 1500, ''),
  (2, 'Focus', 6, 'Channel energy into a sharper, magical hit.', 1200, '')
ON CONFLICT ("Id") DO NOTHING;

-- How each skill scales (Strength = 0, Intellect = 2 per EAttribute).
INSERT INTO "SkillDamageMultipliers" ("SkillId", "AttributeId", "Multiplier") VALUES
  (0, 0, 1.0),
  (1, 0, 1.0),
  (2, 2, 1.0)
ON CONFLICT ("SkillId", "AttributeId") DO NOTHING;

-- A starter class (id 0). Character creation now requires a class (#1221): both signup and
-- add-character send a classId, which the API rejects unless it resolves to a live class, so the
-- suite's signup flow needs class 0 to exist. Its kit is the starter skills 0/1/2 (the skills new
-- players still receive) and a uniform base spread of 5 across the six core attributes (ids 0-5),
-- mirroring the former flat starting allocation so a fresh e2e character is playable. The signature
-- passive is a no-op (amount 0, Additive = 1 per EModifierType).
INSERT INTO "Classes" ("Id", "Name", "Description", "Word", "PassiveAttributeId", "PassiveAmount", "PassiveScalingAttributeId", "PassiveScalingAmount", "PassiveModifierType", "RetiredAt") VALUES
  (0, 'Adventurer', 'A versatile starting archetype.', 'aenkor', 0, 0, NULL, 0, 1, NULL)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO "ClassStarterSkills" ("ClassId", "SkillId") VALUES
  (0, 0),
  (0, 1),
  (0, 2)
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

-- A couple of low-level enemies to populate the zone (and the Enemies admin catalogue), plus a
-- dedicated zone boss (IsBoss). The boss is NOT part of the random ZoneEnemies spawn table — it is
-- fought only via the zone's BossEnemyId (set below) through the deterministic "Challenge Boss"
-- action. Inserted before the Zones so the FK_Zones_Enemies_BossEnemyId reference resolves.
INSERT INTO "Enemies" ("Id", "Name", "IsBoss") VALUES
  (0, 'Forest Slime', false),
  (1, 'Wild Boar', false),
  (2, 'Direboar Alpha', true)
ON CONFLICT ("Id") DO NOTHING;

-- Enemy attribute distributions (Strength = 0, Endurance = 1 per EAttribute). The boss (2) is a
-- deliberately glass-cannon bruiser — Strength only — so at its fought level it hits hard but has
-- modest health. That keeps the real-time e2e boss fight short while a fresh level-1 player still
-- wins it deterministically (the boss never out-damages the player's health pool). These numbers
-- are e2e scaffolding tuned for a quick, winnable fight, not gameplay balance.
INSERT INTO "AttributeDistributions" ("EnemyId", "AttributeId", "BaseAmount", "AmountPerLevel") VALUES
  (0, 0, 5, 1.0),
  (0, 1, 5, 1.0),
  (1, 0, 7, 1.5),
  (1, 1, 6, 1.0),
  (2, 0, 1, 1.0)
ON CONFLICT ("EnemyId", "AttributeId") DO NOTHING;

-- Give the enemies a skill so battles resolve. The boss brings its full authored loadout (Strike +
-- Cleave), which the deterministic "Challenge Boss" path fights it with in full (SelectAllBattleSkills).
INSERT INTO "EnemySkills" ("EnemyId", "SkillId") VALUES
  (0, 0),
  (1, 0),
  (2, 0),
  (2, 1)
ON CONFLICT ("EnemyId", "SkillId") DO NOTHING;

-- The starter zone (Player.CurrentZoneId defaults to 0) is always open and hosts a dedicated boss
-- (enemy 2, fought at the zone's top level via BossLevel), so a brand-new player sees the fight
-- screen's "Challenge Boss" affordance light up. The next zone is gated behind the challenge above
-- (UnlockChallengeId = 0) and has no boss (BossLevel = 1 is the column's NOT NULL default and is
-- meaningless without a BossEnemyId).
INSERT INTO "Zones" ("Id", "Name", "Description", "Order", "LevelMin", "LevelMax", "BossEnemyId", "BossLevel", "UnlockChallengeId") VALUES
  (0, 'Verdant Hollow', 'A quiet glade where new heroes cut their teeth.', 0, 1, 10, 2, 10, NULL),
  (1, 'Ashen Wastes', 'A scorched expanse — sealed until Verdant Hollow is cleared.', 1, 8, 18, NULL, 1, 0)
ON CONFLICT ("Id") DO NOTHING;

-- Place the (non-boss) enemies in the starter zone (equal spawn weight). The boss is excluded — it
-- is challenged explicitly, not rolled into the random idle encounter table.
INSERT INTO "ZoneEnemies" ("ZoneId", "EnemyId", "Weight") VALUES
  (0, 0, 1),
  (0, 1, 1)
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
