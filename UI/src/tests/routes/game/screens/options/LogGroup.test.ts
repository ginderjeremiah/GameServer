import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { ELogType } from '$lib/api';
import type { LogGroupDef, LogTypeDef, OptionsView } from '$routes/game/screens/options/options-view.svelte';

import LogGroup from '$routes/game/screens/options/LogGroup.svelte';

afterEach(cleanup);

const group: LogGroupDef = { key: 'combat', label: 'Combat' };

const types: LogTypeDef[] = [
	{ id: ELogType.Damage, group: 'combat', name: 'Combat Damage', desc: 'Hits.', glyph: 'hit', color: '#aaa' },
	{
		id: ELogType.EnemyDefeated,
		group: 'combat',
		name: 'Enemy Defeated',
		desc: 'Victories.',
		glyph: 'kill',
		color: '#bbb'
	}
];

const makeView = (
	onMap: Partial<Record<ELogType, boolean>> = {},
	dirtyMap: Partial<Record<ELogType, boolean>> = {}
): OptionsView =>
	({
		isOn: vi.fn((id: ELogType) => (id in onMap ? onMap[id] : true)),
		isDirtyId: vi.fn((id: ELogType) => dirtyMap[id] ?? false),
		setOne: vi.fn(),
		setMany: vi.fn()
	}) as unknown as OptionsView;

describe('LogGroup — rendering', () => {
	it('renders the group label', () => {
		render(LogGroup, { props: { group, types, view: makeView() } });
		expect(screen.getByText('Combat')).toBeTruthy();
	});

	it('shows the enabled/total count', () => {
		render(LogGroup, { props: { group, types, view: makeView() } });
		// Both types on by default: 2/2
		expect(screen.getByText('2/2')).toBeTruthy();
	});

	it('reflects the correct count when one type is disabled', () => {
		render(LogGroup, { props: { group, types, view: makeView({ [ELogType.Damage]: false }) } });
		expect(screen.getByText('1/2')).toBeTruthy();
	});

	it('renders child LogTypeRow entries when expanded (default)', () => {
		render(LogGroup, { props: { group, types, view: makeView() } });
		expect(screen.getByText('Combat Damage')).toBeTruthy();
		expect(screen.getByText('Enemy Defeated')).toBeTruthy();
	});
});

describe('LogGroup — expand/collapse', () => {
	it('hides child rows after clicking the header toggle', async () => {
		const { container } = render(LogGroup, { props: { group, types, view: makeView() } });
		await fireEvent.click(container.querySelector('.header-toggle')!);
		expect(screen.queryByText('Combat Damage')).toBeNull();
	});

	it('restores child rows after clicking the header toggle twice', async () => {
		const { container } = render(LogGroup, { props: { group, types, view: makeView() } });
		await fireEvent.click(container.querySelector('.header-toggle')!);
		await fireEvent.click(container.querySelector('.header-toggle')!);
		expect(screen.getByText('Combat Damage')).toBeTruthy();
	});
});

describe('LogGroup — group toggle', () => {
	it('calls view.setMany with all type ids and false when all are on', async () => {
		const view = makeView({ [ELogType.Damage]: true, [ELogType.EnemyDefeated]: true });
		render(LogGroup, { props: { group, types, view } });
		await fireEvent.click(screen.getByTestId('group-toggle-combat'));
		expect(view.setMany).toHaveBeenCalledWith([ELogType.Damage, ELogType.EnemyDefeated], false);
	});

	it('calls view.setMany with all type ids and true when all are off', async () => {
		const view = makeView({ [ELogType.Damage]: false, [ELogType.EnemyDefeated]: false });
		render(LogGroup, { props: { group, types, view } });
		await fireEvent.click(screen.getByTestId('group-toggle-combat'));
		// allOn is false → !allOn is true
		expect(view.setMany).toHaveBeenCalledWith([ELogType.Damage, ELogType.EnemyDefeated], true);
	});
});
