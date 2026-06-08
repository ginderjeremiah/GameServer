import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, fireEvent, cleanup, screen } from '@testing-library/svelte';
import AdminSidebar from '$routes/admin/AdminSidebar.svelte';
import type { AdminGroupDef, AdminToolDef } from '$routes/admin/workbench/nav';

afterEach(cleanup);

const groups: AdminGroupDef[] = [
	{ key: 'combat', label: 'Combat' },
	{ key: 'world', label: 'World' }
];

const tools: AdminToolDef[] = [
	{ key: 'enemies', label: 'Enemies', group: 'combat', glyph: 'skull' },
	{ key: 'skills', label: 'Skills', group: 'combat', glyph: 'bolt' },
	{ key: 'zones', label: 'Zones', group: 'world', glyph: 'map' }
];

const baseProps = () => ({
	tools,
	groups,
	active: 'enemies',
	onNavigate: vi.fn(),
	onBackToGame: vi.fn()
});

describe('AdminSidebar', () => {
	it('renders the sidebar container', () => {
		render(AdminSidebar, { props: baseProps() });
		expect(screen.getByTestId('admin-sidebar')).toBeTruthy();
	});

	it('renders one nav item per tool', () => {
		render(AdminSidebar, { props: baseProps() });

		for (const tool of tools) {
			expect(screen.getByTestId(`admin-tool-${tool.key}`)).toBeTruthy();
		}
	});

	it('omits a group with no tools', () => {
		const combatOnly = tools.filter((t) => t.group === 'combat');
		render(AdminSidebar, { props: { ...baseProps(), tools: combatOnly } });

		expect(screen.queryByTestId('admin-tool-zones')).toBeNull();
		expect(screen.queryByText('World')).toBeNull();
	});

	it('marks the active tool', () => {
		render(AdminSidebar, { props: { ...baseProps(), active: 'skills' } });

		expect(screen.getByTestId('admin-tool-skills').classList.contains('active')).toBe(true);
		expect(screen.getByTestId('admin-tool-enemies').classList.contains('active')).toBe(false);
	});

	it('calls onNavigate when a tool is clicked', async () => {
		const props = baseProps();
		render(AdminSidebar, { props });

		await fireEvent.click(screen.getByTestId('admin-tool-skills'));
		expect(props.onNavigate).toHaveBeenCalledWith('skills');
	});

	it('calls onBackToGame from the return-to-game control', async () => {
		const props = baseProps();
		render(AdminSidebar, { props });

		await fireEvent.click(screen.getByTestId('admin-return-to-game'));
		expect(props.onBackToGame).toHaveBeenCalledOnce();
	});

	it('shows the tool count in the footer', () => {
		render(AdminSidebar, { props: baseProps() });
		expect(screen.getByText(`${tools.length} tools`)).toBeTruthy();
	});

	it('starts collapsed and expands when pinned', () => {
		const { unmount } = render(AdminSidebar, { props: baseProps() });
		expect(screen.getByTestId('admin-sidebar').classList.contains('expanded')).toBe(false);
		unmount();

		render(AdminSidebar, { props: { ...baseProps(), pinned: true } });
		const sidebar = screen.getByTestId('admin-sidebar');
		expect(sidebar.classList.contains('expanded')).toBe(true);
		expect(screen.getByTestId('admin-pin-button').classList.contains('pinned')).toBe(true);
	});

	it('toggles the pinned state when the pin button is clicked', async () => {
		render(AdminSidebar, { props: { ...baseProps(), pinned: true } });

		const sidebar = screen.getByTestId('admin-sidebar');
		await fireEvent.click(screen.getByTestId('admin-pin-button'));

		// Unpinning while not hovering collapses the rail back to the spacer width.
		expect(sidebar.classList.contains('expanded')).toBe(false);
	});
});
