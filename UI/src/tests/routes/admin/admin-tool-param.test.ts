import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent, waitFor } from '@testing-library/svelte';
import { SvelteURL } from 'svelte/reactivity';
import AdminSidebarStub from './page/AdminSidebarStub.svelte';
import WorkbenchStub from './page/WorkbenchStub.svelte';
import DeadLetterConsoleStub from './page/DeadLetterConsoleStub.svelte';
import ContentHealthConsoleStub from './page/ContentHealthConsoleStub.svelte';
import ProgressionStub from './page/ProgressionStub.svelte';

/*
 * #2208: the active admin tool is derived from the `tool` query param instead of being held as its
 * own local state — so a refresh or a shared deep link (e.g. `/admin?tool=paths`) lands on the right
 * tool, and switching tools just writes the URL. This suite pins: deriving the initial tool from the
 * URL (including an unrecognized key falling back to the default), and that a tool switch writes the
 * param via a `replaceState` `goto` — no new history entry — omitting the param for the default tool.
 *
 * The URL mock must be reactive (a `SvelteURL`, matching layout.svelte.test.ts's pattern) since
 * `active` is `$derived` off `page.url.searchParams`.
 */

const { goto, page } = vi.hoisted(() => ({
	goto: vi.fn(() => Promise.resolve()),
	page: { url: new URL('http://localhost/admin') as URL }
}));

vi.mock('$app/navigation', () => ({ goto, beforeNavigate: vi.fn() }));
vi.mock('$app/paths', () => ({ resolve: (path: string) => path }));
vi.mock('$app/state', () => ({ page }));
vi.mock('$stores', () => ({ staticData: {}, toastError: vi.fn(), dangerModal: vi.fn() }));
vi.mock('$routes/admin/admin-access', () => ({ ensureAdminAccess: vi.fn(() => true) }));
vi.mock('$routes/admin/workbench/reference.svelte', () => ({
	reference: { loaded: true, load: vi.fn(async () => {}) }
}));
vi.mock('$routes/admin/workbench/entities', () => ({
	entityByKey: (key: string) =>
		(
			({
				enemies: { key: 'enemies', label: 'Enemies' },
				items: { key: 'items', label: 'Items' }
			}) as Record<string, { key: string; label: string }>
		)[key],
	groupLabelFor: () => 'Group'
}));
vi.mock('$routes/admin/workbench/nav', () => ({
	adminGroups: [],
	adminTools: [
		{ key: 'enemies', label: 'Enemies', group: 'combat', glyph: 'sword' },
		{ key: 'items', label: 'Items', group: 'items', glyph: 'sword' },
		{ key: 'paths', label: 'Paths', group: 'progression', glyph: 'rune' }
	],
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

beforeEach(() => {
	goto.mockClear();
	page.url = new SvelteURL('http://localhost/admin');
});

afterEach(cleanup);

describe('Admin page — tool deep-linking (#2208)', () => {
	it('lands on the tool named by the `tool` query param on mount', async () => {
		page.url = new SvelteURL('http://localhost/admin?tool=items');

		render(AdminPage);

		await waitFor(() => expect(screen.getByTestId('workbench-stub').textContent).toBe('Items'));
	});

	it('falls back to the default tool for an unrecognized key', async () => {
		page.url = new SvelteURL('http://localhost/admin?tool=bogus');

		render(AdminPage);

		await waitFor(() => expect(screen.getByTestId('workbench-stub').textContent).toBe('Enemies'));
	});

	it('lands on a non-entity tool (e.g. Progression) named by the query param', async () => {
		page.url = new SvelteURL('http://localhost/admin?tool=paths');

		render(AdminPage);

		await waitFor(() => expect(screen.getByTestId('progression-stub')).toBeTruthy());
	});

	it('writes the tool to the URL via a replaceState navigation when switching tools', async () => {
		render(AdminPage);
		await waitFor(() => expect(screen.getByTestId('workbench-stub').textContent).toBe('Enemies'));

		await fireEvent.click(screen.getByTestId('nav-items'));

		await waitFor(() =>
			expect(goto).toHaveBeenCalledWith('/admin?tool=items', {
				replaceState: true,
				keepFocus: true,
				noScroll: true
			})
		);
	});

	it('omits the query param entirely when switching back to the default tool', async () => {
		page.url = new SvelteURL('http://localhost/admin?tool=items');
		render(AdminPage);
		await waitFor(() => expect(screen.getByTestId('workbench-stub').textContent).toBe('Items'));

		await fireEvent.click(screen.getByTestId('nav-enemies'));

		await waitFor(() =>
			expect(goto).toHaveBeenCalledWith('/admin', {
				replaceState: true,
				keepFocus: true,
				noScroll: true
			})
		);
	});
});
