import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, fireEvent, cleanup, screen } from '@testing-library/svelte';
import NavSidebar from './NavSidebar.svelte';

vi.mock('$lib/engine', () => ({
	logicEngine: { tickRate: 0 },
	renderEngine: { tickRate: 0 },
}));

afterEach(cleanup);

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
	{ key: 'admin', label: 'Admin', group: 'admin', built: true },
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
});
