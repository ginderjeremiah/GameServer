import { expect, test } from '@playwright/test';
import { createFirstCharacter, selectFirstCharacter, signUp } from './helpers';

// The character-select screen sits between login and the loading screen. Signup creates the account
// only (#1256), so a freshly signed-up account lands here with no characters and creates its first one
// through the class picker — the single class-selection surface — before entering the game.
test.describe('Character select', () => {
	test('signup lands on the empty select screen with the create form open', async ({ page }) => {
		await signUp(page, 'sel');

		await expect(page).toHaveURL('/select', { timeout: 10000 });
		await expect(page.getByTestId('select-heading')).toBeVisible();
		// No characters yet, and the create form is opened automatically so the first one can be made.
		await expect(page.getByTestId('player-card')).toHaveCount(0);
		await expect(page.getByTestId('new-name-input')).toBeVisible();
	});

	test('creates the first character and enters as it', async ({ page }) => {
		await signUp(page, 'sel');

		await createFirstCharacter(page);

		// Selecting it proceeds into the game.
		await selectFirstCharacter(page);
		await expect(page.getByTestId('enter-button')).toBeEnabled({ timeout: 10000 });
	});
});
