-- e2e admin provisioning (NOT content).
--
-- The static reference-data content the end-to-end (Playwright) suite needs is now seeded by the API
-- itself on startup from the source-controlled export (content/*.json, DataAccessOptions__SeedContentOnStartup;
-- see docker-compose.yml and docs/backend-content.md "Content export"). This file carries only the e2e admin-role
-- fixture that used to live at the tail of e2e-seed.sql — it is test-harness infrastructure, not game content,
-- so it stays out of the app and is applied by the CI workflow against the e2e Postgres.
--
-- The admin area is role-gated on both ends: the frontend only shows the Admin nav entry / allows the
-- /admin route to users holding the Admin role, and the backend requires that role on every admin
-- endpoint. The e2e suite creates fresh accounts through the real signup flow, which builds the full
-- user+player graph but grants no roles. Rather than hand-seed an admin user (whose password hash and
-- entire player graph would have to be kept in sync with the app), this trigger auto-attaches the seeded
-- Admin role to any account whose username marks it as an admin fixture ('e2eadmin…'). The admin-area
-- tests can then sign in as a genuine admin while still using a unique account per test, keeping them
-- parallel-safe. The role is resolved by name (not a hardcoded id), so it stays correct if the enum is
-- ever renumbered and simply no-ops if the role is absent.
--
-- e2e-only: this never touches production/local databases. Idempotent: CREATE OR REPLACE + DROP ... IF
-- EXISTS make re-running safe.
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
