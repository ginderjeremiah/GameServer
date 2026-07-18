import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, fireEvent, cleanup, screen } from '@testing-library/svelte';
import NavSidebar from '../../../components/nav-menu/NavSidebar.svelte';

const { mockPlayerManager } = vi.hoisted(() => ({
	mockPlayerManager: { lessons: [] as { lessonId: number; unlockedAt: string; readAt?: string }[] }
}));

vi.mock('$lib/engine', () => ({
	logicEngine: { tickRate: 0 },
	renderEngine: { tickRate: 0 },
	playerManager: mockPlayerManager
}));

afterEach(() => {
	cleanup();
	mockPlayerManager.lessons = [];
});

const screens = [
	{ key: 'fight', label: 'Fight', group: 'combat', built: true },
	{ key: 'cardGame', label: 'Card Game', group: 'combat', built: false },
	{ key: 'challenges', label: 'Challenges', group: 'combat', built: true },
	{ key: 'inventory', label: 'Inventory', group: 'character', built: true },
	{ key: 'attributes', label: 'Attributes', group: 'character', built: false },
	{ key: 'stats', label: 'Stats', group: 'character', built: false },
	{ key: 'options', label: 'Options', group: 'settings', built: false },
	{ key: 'help', label: 'Help', group: 'settings', built: false },
	{ key: 'quit', label: 'Quit', group: 'settings', built: false },
	{ key: 'admin', label: 'Admin', group: 'admin', built: true }
];

describe('NavSidebar', () => {
	it('renders sidebar container', () => {
		render(NavSidebar, { props: { screens, active: 'fight', onNavigate: vi.fn() } });
		expect(screen.getByTestId('sidebar')).toBeTruthy();
	});

	it('renders all sidebar nav items with data-testid', () => {
		render(NavSidebar, { props: { screens, active: 'fight', onNavigate: vi.fn() } });

		for (const s of screens) {
			expect(screen.getByTestId(`sidebar-item-${s.key}`)).toBeTruthy();
		}
	});

	it('omits a group with no screens (e.g. the Admin section hidden for non-admins)', () => {
		// The game page filters role-gated screens out before passing them in; the rail should then
		// render neither the admin item nor an empty Admin group header.
		const nonAdmin = screens.filter((s) => s.key !== 'admin');
		render(NavSidebar, { props: { screens: nonAdmin, active: 'fight', onNavigate: vi.fn() } });

		expect(screen.queryByTestId('sidebar-item-admin')).toBeNull();
		expect(screen.queryByText('Admin')).toBeNull();
	});

	it('marks the active item', () => {
		render(NavSidebar, { props: { screens, active: 'inventory', onNavigate: vi.fn() } });

		const inventoryItem = screen.getByTestId('sidebar-item-inventory');
		expect(inventoryItem.classList.contains('active')).toBe(true);

		const fightItem = screen.getByTestId('sidebar-item-fight');
		expect(fightItem.classList.contains('active')).toBe(false);
	});

	it('calls onNavigate when a nav item is clicked', async () => {
		const onNavigate = vi.fn();
		render(NavSidebar, { props: { screens, active: 'fight', onNavigate } });

		await fireEvent.click(screen.getByTestId('sidebar-item-inventory'));
		expect(onNavigate).toHaveBeenCalledWith('inventory');
	});

	it('starts in collapsed state (60px)', () => {
		render(NavSidebar, { props: { screens, active: 'fight', onNavigate: vi.fn() } });

		const sidebar = screen.getByTestId('sidebar');
		expect(sidebar.classList.contains('expanded')).toBe(false);
	});

	it('is expanded and shows the pin button when pinned', () => {
		render(NavSidebar, { props: { screens, active: 'fight', onNavigate: vi.fn(), pinned: true } });

		const sidebar = screen.getByTestId('sidebar');
		expect(sidebar.classList.contains('expanded')).toBe(true);

		const pinButton = screen.getByTestId('pin-button');
		expect(pinButton.classList.contains('pinned')).toBe(true);
	});

	it('expands on keyboard focus so a Tab-only user can reach the pin button', async () => {
		render(NavSidebar, { props: { screens, active: 'fight', onNavigate: vi.fn() } });

		const sidebar = screen.getByTestId('sidebar');
		expect(sidebar.classList.contains('expanded')).toBe(false);
		expect(screen.queryByTestId('pin-button')).toBeNull();

		await fireEvent.focusIn(screen.getByTestId('sidebar-item-fight'));
		expect(sidebar.classList.contains('expanded')).toBe(true);
		expect(screen.getByTestId('pin-button')).toBeTruthy();

		await fireEvent.focusOut(screen.getByTestId('sidebar-item-fight'));
		expect(sidebar.classList.contains('expanded')).toBe(false);
	});

	it('toggles the pinned state when the pin button is clicked', async () => {
		render(NavSidebar, { props: { screens, active: 'fight', onNavigate: vi.fn(), pinned: true } });

		const sidebar = screen.getByTestId('sidebar');
		await fireEvent.click(screen.getByTestId('pin-button'));

		// Unpinning while not hovering collapses the rail back to the spacer width.
		expect(sidebar.classList.contains('expanded')).toBe(false);
	});

	describe('unread lesson badge', () => {
		it('shows no badge and the wip badge when there are no unread lessons', () => {
			render(NavSidebar, { props: { screens, active: 'fight', onNavigate: vi.fn() } });

			const helpItem = screen.getByTestId('sidebar-item-help');
			expect(helpItem.textContent).toContain('wip');
		});

		it('shows the unread count on the Help item, superseding the wip badge, when lessons are unread', () => {
			mockPlayerManager.lessons = [
				{ lessonId: 1, unlockedAt: '2026-01-01T00:00:00Z' },
				{ lessonId: 2, unlockedAt: '2026-01-01T00:00:00Z', readAt: '2026-01-01T00:05:00Z' },
				{ lessonId: 3, unlockedAt: '2026-01-01T00:00:00Z' }
			];

			render(NavSidebar, { props: { screens, active: 'fight', onNavigate: vi.fn() } });

			const helpItem = screen.getByTestId('sidebar-item-help');
			expect(helpItem.textContent).toContain('2');
			expect(helpItem.textContent).not.toContain('wip');
		});

		it('does not show a badge on other wip screens', () => {
			mockPlayerManager.lessons = [{ lessonId: 1, unlockedAt: '2026-01-01T00:00:00Z' }];

			render(NavSidebar, { props: { screens, active: 'fight', onNavigate: vi.fn() } });

			const optionsItem = screen.getByTestId('sidebar-item-options');
			expect(optionsItem.textContent).toContain('wip');
		});
	});
});
