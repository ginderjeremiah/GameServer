import { expect, test } from '@playwright/test';
import { createFirstCharacter, selectFirstCharacter, shortUsername, TEST_PASSWORD, waitForLoginReady } from './helpers';

// The character-select screen sits between login and the loading screen. Signup creates the account
// only (#1256), so a freshly signed-up account lands here with no characters and creates its first one
// through the class picker — the single class-selection surface — before entering the game.
test.describe('Character select', () => {
	const signUp = async (page: import('@playwright/test').Page) => {
		await page.goto('/');
		await waitForLoginReady(page);

		await page.getByTestId('mode-toggle').click();
		const username = shortUsername('sel');
		await page.getByTestId('username-input').fill(username);
		await page.getByTestId('password-input').fill(TEST_PASSWORD);
		await page.getByTestId('confirm-input').fill(TEST_PASSWORD);
		await page.getByTestId('submit-button').click();
	};

	test('signup lands on the empty select screen with the create form open', async ({ page }) => {
		await signUp(page);

		await expect(page).toHaveURL('/select', { timeout: 10000 });
		await expect(page.getByTestId('select-heading')).toBeVisible();
		// No characters yet, and the create form is opened automatically so the first one can be made.
		await expect(page.getByTestId('player-card')).toHaveCount(0);
		await expect(page.getByTestId('new-name-input')).toBeVisible();
	});

	test('creates the first character and enters as it', async ({ page }) => {
		await signUp(page);

		await createFirstCharacter(page);

		// Selecting it proceeds into the game.
		await selectFirstCharacter(page);
		await expect(page.getByTestId('enter-button')).toBeEnabled({ timeout: 10000 });
	});
});
