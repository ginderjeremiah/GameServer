import { expect, test } from '@playwright/test';
import { createAdminAndStartGame, gotoAdmin } from './helpers';

test.describe('Admin workbench', () => {
	test('renders the workbench with seeded records', async ({ page }) => {
		await createAdminAndStartGame(page);
		await gotoAdmin(page);

		// The workbench opens on the Enemies catalogue, populated from seed reference data.
		await expect(page.getByTestId('workbench-title')).toHaveText('Enemies');
		await expect(page.getByTestId('workbench-row').first()).toBeVisible({ timeout: 10000 });
	});

	test('switching entities via the sidebar swaps the active catalogue', async ({ page }) => {
		await createAdminAndStartGame(page);
		await gotoAdmin(page);
		await expect(page.getByTestId('workbench-row').first()).toBeVisible({ timeout: 10000 });

		await page.getByTestId('admin-tool-skills').click();

		await expect(page.getByTestId('workbench-title')).toHaveText('Skills');
		await expect(page.getByTestId('workbench-row').first()).toBeVisible({ timeout: 10000 });
	});

	test('New adds an unsaved record to the catalogue', async ({ page }) => {
		await createAdminAndStartGame(page);
		await gotoAdmin(page);
		await expect(page.getByTestId('workbench-row').first()).toBeVisible({ timeout: 10000 });

		const rows = page.getByTestId('workbench-row');
		const before = await rows.count();

		await page.getByTestId('workbench-new').click();

		await expect(rows).toHaveCount(before + 1);
		// The save bar reflects the pending addition.
		await expect(page.getByText('1 unsaved change', { exact: true })).toBeVisible();
	});
});
