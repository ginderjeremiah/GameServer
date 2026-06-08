import { expect, test } from '@playwright/test';
import { createAccountAndLogin, loginExistingUser, waitForLoginReady } from './helpers';

test.describe('Game flow', () => {
	test.describe.configure({ mode: 'serial' });

	let username: string;

	test('can create an account', async ({ page }) => {
		username = await createAccountAndLogin(page, 'gf');
		await expect(page.getByTestId('loading-heading')).toBeVisible();
	});

	test('loading page fetches data and enables enter button', async ({ page }) => {
		await page.goto('/');
		await waitForLoginReady(page);

		await loginExistingUser(page, username);

		const enterButton = page.getByTestId('enter-button');
		await expect(enterButton).toBeEnabled({ timeout: 10000 });

		await enterButton.click();
		await expect(page).toHaveURL('/game', { timeout: 5000 });
	});

	test('game page renders sidebar and battle screen', async ({ page }) => {
		await page.goto('/');
		await waitForLoginReady(page);

		await loginExistingUser(page, username);

		const enterButton = page.getByTestId('enter-button');
		await expect(enterButton).toBeEnabled({ timeout: 10000 });
		await enterButton.click();

		await expect(page).toHaveURL('/game', { timeout: 5000 });

		await expect(page.getByTestId('sidebar')).toBeVisible();
		await expect(page.getByTestId('sidebar-item-fight')).toBeVisible();
		await expect(page.getByTestId('sidebar-item-inventory')).toBeVisible();
		await expect(page.getByTestId('log-panel')).toBeVisible();
	});

	test('locks the next zone until the current zone is cleared', async ({ page }) => {
		await page.goto('/');
		await waitForLoginReady(page);

		await loginExistingUser(page, username);

		const enterButton = page.getByTestId('enter-button');
		await expect(enterButton).toBeEnabled({ timeout: 10000 });
		await enterButton.click();

		await expect(page).toHaveURL('/game', { timeout: 5000 });

		// The fight screen is the default. The seeded second zone (Ashen Wastes) is gated behind
		// clearing the starter zone, which a brand-new player has not done, so the forward arrow is
		// locked and not navigable.
		const zoneNav = page.getByTestId('zone-nav');
		await expect(zoneNav).toBeVisible();
		const forward = page.getByRole('button', { name: 'Next zone locked' });
		await expect(forward).toBeVisible();
		await expect(forward).toBeDisabled();
		await expect(zoneNav).toContainText('Verdant Hollow');
	});

	test('resumes straight into the game on reload once the cache is warm', async ({ page }) => {
		await page.goto('/');
		await waitForLoginReady(page);

		await loginExistingUser(page, username);

		// Let the loading screen finish so every reference-data set is written to the localStorage cache.
		await expect(page.getByTestId('enter-button')).toBeEnabled({ timeout: 10000 });

		// A refresh with a valid session and a warm cache restores the session and skips the loading
		// screen entirely, dropping the player straight back into the game (#106).
		await page.reload();
		await expect(page).toHaveURL('/game', { timeout: 10000 });
		await expect(page.getByTestId('game-screen')).toBeVisible();
	});
});
