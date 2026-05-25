import type { Page } from '@playwright/test';
import { expect } from '@playwright/test';

/**
 * Wait for the login page to fully hydrate (JS loaded + onMount status check complete).
 * Must be called before interacting with the login form, otherwise Svelte bindings
 * and event handlers aren't attached yet.
 */
export async function waitForLoginReady(page: Page) {
	await expect(page.locator('h1')).toHaveText('Login', { timeout: 10000 });
	await page.waitForResponse(
		(r) => r.url().includes('/api/Login/Status'),
		{ timeout: 10000 }
	).catch(() => {});
}

/** DB username column is varchar(20), so keep test usernames short. */
export function shortUsername(prefix: string) {
	return `${prefix}${Date.now() % 1e10}`;
}

export const TEST_PASSWORD = 'Test123!';

export async function createAccountAndLogin(page: Page, prefix = 'e') {
	await page.goto('/');
	await waitForLoginReady(page);

	const username = shortUsername(prefix);
	await page.locator('input[name="username"]').fill(username);
	await page.locator('input[name="password"]').fill(TEST_PASSWORD);
	await page.locator('button', { hasText: 'Create Account' }).click();

	await expect(page).toHaveURL('/loading', { timeout: 10000 });
	return username;
}

export async function createAccountAndStartGame(page: Page, prefix = 'e') {
	const username = await createAccountAndLogin(page, prefix);

	const startButton = page.locator('button', { hasText: 'Start Game' });
	await expect(startButton).toBeEnabled({ timeout: 10000 });
	await startButton.click();
	await expect(page).toHaveURL('/game', { timeout: 5000 });
	return username;
}
