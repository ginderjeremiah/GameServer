import { expect, test } from '@playwright/test';
import { createAccountAndStartGame } from './helpers';

// The dedicated zone boss in the starter zone (Verdant Hollow), seeded on startup from the
// source-controlled content export (content/enemies.json). It is tuned to be deterministically
// beatable by a brand-new player, so this flow always resolves in a victory. Mirror the name authored there.
const BOSS_NAME = 'Direboar Alpha';

test.describe('Challenge Boss', () => {
	// The single critical path for the boss feature: a fresh player finds the boss affordance, engages
	// the heightened boss fight, and wins — clearing the zone. e2e is reserved for important journeys
	// (per frontend.md), so the finer boss states (auto-fight re-challenge, loss, retreat, cleared
	// re-challenge wording) are left to the unit/component suites and only this end-to-end payoff runs.
	test('challenges the seeded zone boss and clears the zone on victory', async ({ page }) => {
		await createAccountAndStartGame(page, 'bs');

		// Fight is the default screen; the starter zone's seeded boss lights up the always-available
		// Challenge affordance between the zone nav and the arena.
		await expect(page.getByTestId('fight-screen')).toBeVisible();
		const trigger = page.getByTestId('boss-trigger');
		await expect(trigger).toBeVisible({ timeout: 10000 });
		await expect(trigger).toContainText(BOSS_NAME);

		// Challenge engages the dedicated, heightened boss fight: the trigger is replaced by the in-fight
		// boss bar, and the normal enemy card by the gold boss card.
		await page.getByTestId('challenge-boss').click();
		await expect(page.getByTestId('boss-bar')).toBeVisible({ timeout: 10000 });
		await expect(page.getByTestId('boss-card')).toBeVisible();

		// The seeded boss is beatable, so the fight resolves in a victory: the Zone-Cleared overlay plays
		// and carries the "Cleared" seal. (Auto-fight is off by default, so the boss is not immediately
		// re-challenged — the victory moment is observable.) The real-time fight takes a few seconds, so
		// the overlay's appearance gets a wider budget than the affordance swap above.
		const overlay = page.getByTestId('zone-cleared-overlay');
		await expect(overlay).toBeVisible({ timeout: 15000 });
		await expect(overlay).toContainText(`${BOSS_NAME} defeated`);
		await expect(overlay.getByTestId('cleared-seal')).toBeVisible();
	});
});
