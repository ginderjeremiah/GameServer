import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';

/**
 * Wait for the login page to fully hydrate before interacting with it — otherwise Svelte bindings
 * and event handlers aren't attached yet and a click on the (server-rendered) form is dropped.
 *
 * The root layout sets `data-hydrated="true"` from its onMount, which runs after every child's
 * onMount, so this resolves as soon as the page is actually interactive. (It previously waited on a
 * `/api/Login/Status` response, but that request only fires when a stored token exists — never in a
 * fresh test context — so the wait always burned its full timeout.)
 */
export async function waitForLoginReady(page: Page) {
	await expect(page.getByTestId('login-heading')).toBeVisible({ timeout: 10000 });
	await expect(page.locator('[data-hydrated="true"]')).toBeAttached({ timeout: 10000 });
}

/**
 * Build a unique test username. The DB column is varchar(20), so this stays short while
 * combining a time component with randomness — parallel workers and Playwright retries can
 * otherwise produce the same `Date.now()` value and collide on "username already exists".
 */
export function shortUsername(prefix: string) {
	const time = (Date.now() % 1e7).toString().padStart(7, '0');
	const rand = Math.floor(Math.random() * 1e4)
		.toString()
		.padStart(4, '0');
	return `${prefix}${time}${rand}`;
}

export const TEST_PASSWORD = 'Test123!';

/**
 * From the character-select screen (where login/signup now lands), enter as the account's first
 * character and land on `/loading`.
 *
 * A rapid re-login of the same user can trip the "active session" takeover warning: the prior
 * session's socket may still be within its presence TTL (notably on WebKit, whose browser-context
 * teardown doesn't cleanly close the socket, so the server hasn't yet cleared the presence key). The
 * takeover modal is confirmed if it appears; if the prior session was already torn down cleanly (as on
 * Chromium/Firefox) no modal shows and we proceed straight through.
 */
export async function selectFirstCharacter(page: Page) {
	await expect(page).toHaveURL('/select', { timeout: 10000 });
	await page.getByTestId('player-card').first().click();

	try {
		await page.locator('[data-modal-primary]').click({ timeout: 3000 });
	} catch {
		// No takeover prompt appeared — the prior session had already been cleaned up.
	}

	await expect(page).toHaveURL('/loading', { timeout: 10000 });
}

/**
 * From the (empty) select screen a freshly-signed-up account lands on, create the first character —
 * signup no longer creates one (#1256). The create form is auto-opened for an account with no
 * characters; fall back to the "+ New character" affordance if it isn't already open. Leaves exactly
 * one player card on the screen.
 */
export async function createFirstCharacter(page: Page, name = 'Hero') {
	await expect(page).toHaveURL('/select', { timeout: 10000 });

	const nameInput = page.getByTestId('new-name-input');
	if (!(await nameInput.isVisible())) {
		await page.getByTestId('show-create').click();
	}
	// A class is required to create, so wait for the picker to load (it defaults to the first class)
	// before submitting — the Create button stays disabled until a class is selected.
	await expect(page.getByTestId('class-picker')).toBeVisible({ timeout: 10000 });
	await nameInput.fill(name);
	await page.getByTestId('create-form').getByTestId('submit-button').click();

	await expect(page.getByTestId('player-card')).toHaveCount(1, { timeout: 10000 });
}

export async function createAccountAndLogin(page: Page, prefix = 'e') {
	await page.goto('/');
	await waitForLoginReady(page);

	// Switch to signup mode
	await page.getByTestId('mode-toggle').click();

	const username = shortUsername(prefix);
	await page.getByTestId('username-input').fill(username);
	await page.getByTestId('password-input').fill(TEST_PASSWORD);
	await page.getByTestId('confirm-input').fill(TEST_PASSWORD);
	await page.getByTestId('submit-button').click();

	// Signup lands on the select screen with no characters; create the first one, then enter as it.
	await createFirstCharacter(page);
	await selectFirstCharacter(page);
	return username;
}

/**
 * Log an existing account in from the login screen, pick its first character, and land on `/loading`.
 */
export async function loginExistingUser(page: Page, username: string) {
	await page.getByTestId('username-input').fill(username);
	await page.getByTestId('password-input').fill(TEST_PASSWORD);
	await page.getByTestId('submit-button').click();

	await selectFirstCharacter(page);
}

export async function createAccountAndStartGame(page: Page, prefix = 'e') {
	const username = await createAccountAndLogin(page, prefix);

	const enterButton = page.getByTestId('enter-button');
	await expect(enterButton).toBeEnabled({ timeout: 10000 });
	await enterButton.click();
	// Entering the game loads battle/zone state before /game renders, which on the slower engines
	// (WebKit/Firefox) under parallel load routinely takes longer than 5s and flakes. Use the same
	// 10s budget as the other navigations in this file.
	await expect(page).toHaveURL('/game', { timeout: 10000 });
	return username;
}

/**
 * Like {@link createAccountAndStartGame}, but the account is provisioned with the Admin role so it
 * can reach the (role-gated) admin area. The admin area is gated on the frontend (the Admin nav
 * entry + the /admin route check the role) and the backend (every admin endpoint requires it), and
 * the signup flow grants no roles, so the e2e DB seed auto-grants Admin to any account whose
 * username starts with `e2eadmin` (see `e2e-seed.sql`). Each call still creates a unique account,
 * so admin tests stay parallel-safe (no shared session getting replaced out from under them).
 */
export async function createAdminAndStartGame(page: Page) {
	return createAccountAndStartGame(page, 'e2eadmin');
}

/**
 * Open the admin route from the in-game sidebar. Two things make a naive click flaky on the slower
 * engines (Firefox/WebKit):
 *   1. The /game page mounts the battle/render engine and opens its socket on entry, so a click
 *      fired while that's still settling can be swallowed without triggering navigation.
 *   2. The navigation is client-side (`goto('/admin')`); under load it can take more than a couple
 *      of seconds, and the old 2s retry window expired mid-navigation so the loop re-clicked and
 *      interrupted it — leaving the page stuck on /game until the retry budget ran out.
 * So: wait for the game screen to render first, then retry the click with a window wide enough for
 * the navigation to actually complete before re-clicking.
 */
export async function gotoAdmin(page: Page) {
	await expect(page.getByTestId('game-screen')).toBeVisible({ timeout: 10000 });

	await expect(async () => {
		if (!new URL(page.url()).pathname.startsWith('/admin')) {
			await page.getByTestId('sidebar-item-admin').click();
		}
		await expect(page).toHaveURL('/admin', { timeout: 5000 });
	}).toPass({ timeout: 15000 });

	await expect(page.getByTestId('admin-screen')).toBeVisible();
}
