import { expect, test } from '@playwright/test';
import { createAccountAndStartGame } from './helpers';

async function loginAndGoToAdmin(page: import('@playwright/test').Page) {
	await createAccountAndStartGame(page, 'ad');
	await page.getByText('Admin', { exact: true }).click();
	await expect(page).toHaveURL('/admin', { timeout: 5000 });
}

test.describe('Admin', () => {
	test('renders table editor with data', async ({ page }) => {
		await loginAndGoToAdmin(page);

		await expect(page.locator('table')).toBeVisible({ timeout: 5000 });
		await expect(page.locator('thead')).toBeVisible();

		const rows = page.locator('tbody tr');
		await expect(rows.first()).toBeVisible({ timeout: 5000 });
	});

	test('switching between admin tools', async ({ page }) => {
		await loginAndGoToAdmin(page);

		const addEditSkills = page.getByText('Add/Edit Skills');
		await expect(addEditSkills).toBeVisible({ timeout: 3000 });
		await addEditSkills.click({ force: true });

		await expect(page.locator('table')).toBeVisible({ timeout: 5000 });
	});

	test('add row button works', async ({ page }) => {
		await loginAndGoToAdmin(page);

		await expect(page.locator('tbody tr').first()).toBeVisible({ timeout: 5000 });
		const initialRowCount = await page.locator('tbody tr').count();

		await page.locator('button', { hasText: 'Add Row' }).click();

		const newRowCount = await page.locator('tbody tr').count();
		expect(newRowCount).toBe(initialRowCount + 1);
	});
});
