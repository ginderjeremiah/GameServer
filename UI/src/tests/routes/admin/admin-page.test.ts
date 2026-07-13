import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent, waitFor } from '@testing-library/svelte';
import AdminSidebarStub from './page/AdminSidebarStub.svelte';
import WorkbenchStub from './page/WorkbenchStub.svelte';
import DeadLetterConsoleStub from './page/DeadLetterConsoleStub.svelte';
import ContentHealthConsoleStub from './page/ContentHealthConsoleStub.svelte';
import ProgressionStub from './page/ProgressionStub.svelte';

/*
 * `admin/+page.svelte` gates every workbench tool behind `reference.loaded` (the shared reference
 * catalogues backing every entity's select options, tag UI, and derived spawn shares). Before #1913
 * a failed `reference.load()` left every tool spinning forever with only a one-shot toast — this
 * pins the fix: an error state with a Refresh action that retries the real load.
 *
 * `reference` itself (and its `loaded` $state) is kept real so the fix's reactivity is genuinely
 * exercised; only its transports (`fetchSocketData`/`ApiRequest`) are stubbed, mirroring
 * reference-load.test.ts. The nav/entities modules and every heavy child (Workbench, AdminSidebar,
 * the ops consoles, Progression) are replaced with minimal stubs so only the default 'enemies' tool's
 * reference-gate wiring is under test.
 */

const { staticData, mockFetchSocket, mockGet, ensureAdminAccessMock } = vi.hoisted(() => ({
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: {} as any,
	mockFetchSocket: vi.fn(),
	mockGet: vi.fn(),
	ensureAdminAccessMock: vi.fn(() => true)
}));

vi.mock('$app/navigation', () => ({ goto: vi.fn(), beforeNavigate: vi.fn() }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path }));
vi.mock('$stores', () => ({ staticData, toastError: vi.fn(), dangerModal: vi.fn() }));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	class ApiRequest {
		static get = mockGet;
		static post = vi.fn();
	}
	return { ...actual, ApiRequest, fetchSocketData: mockFetchSocket };
});
vi.mock('$routes/admin/admin-access', () => ({ ensureAdminAccess: ensureAdminAccessMock }));
vi.mock('$routes/admin/workbench/entities', () => ({
	entityByKey: (key: string) => (key === 'enemies' ? { key: 'enemies', label: 'Enemies' } : undefined),
	groupLabelFor: () => 'Combat'
}));
vi.mock('$routes/admin/workbench/nav', () => ({
	adminGroups: [],
	adminTools: [],
	CONTENT_HEALTH_TOOL_KEY: 'content-health',
	DEAD_LETTERS_TOOL_KEY: 'dead-letters',
	PROGRESSION_TOOL_KEY: 'paths',
	SOCKET_DEAD_LETTERS_TOOL_KEY: 'socket-dead-letters'
}));
vi.mock('$routes/admin/AdminSidebar.svelte', () => ({ default: AdminSidebarStub }));
vi.mock('$routes/admin/workbench/Workbench.svelte', () => ({ default: WorkbenchStub }));
vi.mock('$routes/admin/ops/DeadLetterConsole.svelte', () => ({ default: DeadLetterConsoleStub }));
vi.mock('$routes/admin/ops/ContentHealthConsole.svelte', () => ({ default: ContentHealthConsoleStub }));
vi.mock('$routes/admin/workbench/progression/Progression.svelte', () => ({ default: ProgressionStub }));

import AdminPage from '$routes/admin/+page.svelte';

// One distinct payload per socket command, matching reference-load.test.ts.
const SOCKET_SETS: Record<string, { id: number; name: string }[]> = {
	GetEnemies: [{ id: 0, name: 'Cave Bat' }],
	GetSkills: [{ id: 0, name: 'Cleave' }],
	GetZones: [{ id: 0, name: 'Verdant Hollow' }],
	GetItems: [{ id: 0, name: 'Iron Helm' }],
	GetItemMods: [{ id: 0, name: 'Sharp' }],
	GetAttributes: [{ id: 0, name: 'Strength' }],
	GetChallengeTypes: [{ id: 1, name: 'Enemies Killed' }],
	GetChallenges: [{ id: 0, name: 'First Blood' }],
	GetPaths: [{ id: 0, name: 'Fire Magic' }],
	GetProficiencies: [{ id: 0, name: 'Fire' }],
	GetLessons: [{ id: 0, name: 'Idle Combat' }],
	GetClasses: [{ id: 0, name: 'Warrior' }],
	GetSkillRecipes: [{ id: 0, name: 'Recipe' }]
};
const TAGS = [{ id: 10, name: 'Fire', tagCategoryId: 100 }];
const TAG_CATEGORIES = [{ id: 100, name: 'Element' }];

beforeEach(() => {
	ensureAdminAccessMock.mockReturnValue(true);
	mockGet
		.mockReset()
		.mockImplementation(async (path: string) =>
			path === 'Tags' ? TAGS : path === 'Tags/TagCategories' ? TAG_CATEGORIES : []
		);
	for (const key of Object.keys(staticData)) {
		delete staticData[key];
	}
});

afterEach(cleanup);

describe('Admin page — reference-data load failure', () => {
	it('shows an error state with a Refresh action instead of spinning forever, and recovers on retry', async () => {
		const { toastError } = await import('$stores');
		// The first socket call (GetEnemies, first in reference.load()'s Promise.all) fails; every
		// subsequent call — including the retry's — succeeds.
		mockFetchSocket
			.mockReset()
			.mockImplementationOnce(async () => {
				throw new Error('reference data unreachable');
			})
			.mockImplementation(async (command: string) => SOCKET_SETS[command] ?? []);

		render(AdminPage);

		await waitFor(() => expect(screen.getByTestId('admin-reference-error')).toBeTruthy());
		expect(screen.getByTestId('admin-reference-error').textContent).toContain('reference data unreachable');
		expect(screen.queryByTestId('workbench-stub')).toBeNull();
		expect(toastError).toHaveBeenCalledWith('reference data unreachable');

		await fireEvent.click(screen.getByText('Refresh'));

		await waitFor(() => expect(screen.getByTestId('workbench-stub')).toBeTruthy());
		expect(screen.queryByTestId('admin-reference-error')).toBeNull();
		expect(screen.getByTestId('workbench-stub').textContent).toBe('Enemies');
	});
});
