import { expect, test } from '@playwright/test';
import { selectFirstCharacter, shortUsername, TEST_PASSWORD, waitForLoginReady } from './helpers';

// The character-select screen sits between login and the loading screen: signup lands here with the
// account's starting character, the player can create additional ones (bounded server-side by the
// per-account cap), and selecting one binds the session and proceeds into the game.
test.describe('Character select', () => {
	test('signup lands on the select screen with the starting character', async ({ page }) => {
		await page.goto('/');
		await waitForLoginReady(page);

		await page.getByTestId('mode-toggle').click();
		const username = shortUsername('sel');
		await page.getByTestId('username-input').fill(username);
		await page.getByTestId('password-input').fill(TEST_PASSWORD);
		await page.getByTestId('confirm-input').fill(TEST_PASSWORD);
		await page.getByTestId('submit-button').click();

		await expect(page).toHaveURL('/select', { timeout: 10000 });
		await expect(page.getByTestId('select-heading')).toBeVisible();
		await expect(page.getByTestId('player-card')).toHaveCount(1);
	});

	test('can create an additional character and enter as it', async ({ page }) => {
		await page.goto('/');
		await waitForLoginReady(page);

		await page.getByTestId('mode-toggle').click();
		const username = shortUsername('sel');
		await page.getByTestId('username-input').fill(username);
		await page.getByTestId('password-input').fill(TEST_PASSWORD);
		await page.getByTestId('confirm-input').fill(TEST_PASSWORD);
		await page.getByTestId('submit-button').click();

		await expect(page).toHaveURL('/select', { timeout: 10000 });

		// Create a second character.
		await page.getByTestId('show-create').click();
		await page.getByTestId('new-name-input').fill('Second Hero');
		await page.getByTestId('create-form').getByTestId('submit-button').click();

		await expect(page.getByTestId('player-card')).toHaveCount(2, { timeout: 10000 });

		// Selecting one proceeds into the game.
		await selectFirstCharacter(page);
		await expect(page.getByTestId('enter-button')).toBeEnabled({ timeout: 10000 });
	});
});
