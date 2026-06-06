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

	await expect(page).toHaveURL('/loading', { timeout: 10000 });
	return username;
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
 * Open the admin route from the in-game sidebar. The sidebar expands on hover with a width
 * transition, so under parallel load a single synthetic click can land on the still-moving
 * button and be dropped without navigating. Retry the click until the route actually changes.
 */
export async function gotoAdmin(page: Page) {
	await expect(async () => {
		if (!new URL(page.url()).pathname.startsWith('/admin')) {
			await page.getByTestId('sidebar-item-admin').click();
		}
		await expect(page).toHaveURL('/admin', { timeout: 2000 });
	}).toPass({ timeout: 15000 });

	await expect(page.getByTestId('admin-screen')).toBeVisible();
}
