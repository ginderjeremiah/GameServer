import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';
import { ELogType } from '$lib/api';
import type { LogTypeDef, OptionsView } from '$routes/game/screens/options/options-view.svelte';

import LogTypeRow from '$routes/game/screens/options/LogTypeRow.svelte';

afterEach(cleanup);

const makeType = (overrides: Partial<LogTypeDef> = {}): LogTypeDef => ({
	id: ELogType.Damage,
	group: 'combat',
	name: 'Combat Damage',
	desc: 'Hits you deal and take during battle.',
	glyph: 'hit',
	color: '#c0d8ff',
	...overrides
});

const makeView = (on: boolean, dirty: boolean): OptionsView =>
	({
		isOn: vi.fn(() => on),
		isDirtyId: vi.fn(() => dirty),
		setOne: vi.fn()
	}) as unknown as OptionsView;

describe('LogTypeRow — content', () => {
	it('renders the type name and description', () => {
		render(LogTypeRow, { props: { type: makeType(), view: makeView(true, false) } });
		expect(screen.getByText('Combat Damage')).toBeTruthy();
		expect(screen.getByText('Hits you deal and take during battle.')).toBeTruthy();
	});

	it('shows the "edited" badge when the type is dirty', () => {
		render(LogTypeRow, { props: { type: makeType(), view: makeView(true, true) } });
		expect(screen.getByText('edited')).toBeTruthy();
	});

	it('does not show the "edited" badge when clean', () => {
		render(LogTypeRow, { props: { type: makeType(), view: makeView(true, false) } });
		expect(screen.queryByText('edited')).toBeNull();
	});
});

describe('LogTypeRow — on/off state', () => {
	it('has the "on" class when the type is enabled', () => {
		const { container } = render(LogTypeRow, { props: { type: makeType(), view: makeView(true, false) } });
		expect(container.querySelector('.log-row')!.classList.contains('on')).toBe(true);
	});

	it('does not have the "on" class when the type is disabled', () => {
		const { container } = render(LogTypeRow, { props: { type: makeType(), view: makeView(false, false) } });
		expect(container.querySelector('.log-row')!.classList.contains('on')).toBe(false);
	});

	it('renders the toggle with the correct testid', () => {
		const type = makeType({ id: ELogType.Exp });
		const { container } = render(LogTypeRow, { props: { type, view: makeView(true, false) } });
		expect(container.querySelector(`[data-testid="toggle-${ELogType.Exp}"]`)).toBeTruthy();
	});
});
