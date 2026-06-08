import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import type { OptionsView } from '$routes/game/screens/options/options-view.svelte';

import SaveBar from '$routes/game/screens/options/SaveBar.svelte';

afterEach(cleanup);

const makeView = (
	overrides: Partial<{ saved: boolean; dirtyCount: number; isDirty: boolean; saving: boolean }>
): OptionsView =>
	({
		saved: false,
		dirtyCount: 0,
		isDirty: false,
		saving: false,
		discard: vi.fn(),
		save: vi.fn(),
		...overrides
	}) as unknown as OptionsView;

describe('SaveBar — status text', () => {
	it('shows "No unsaved changes" when clean', () => {
		render(SaveBar, { props: { view: makeView({}) } });
		expect(screen.getByText('No unsaved changes')).toBeTruthy();
	});

	it('shows the dirty count for multiple changes', () => {
		render(SaveBar, { props: { view: makeView({ dirtyCount: 3, isDirty: true }) } });
		expect(screen.getByTestId('save-status-dirty').textContent).toContain('3 unsaved changes');
	});

	it('uses singular "change" for exactly 1 dirty item', () => {
		render(SaveBar, { props: { view: makeView({ dirtyCount: 1, isDirty: true }) } });
		expect(screen.getByTestId('save-status-dirty').textContent).toContain('1 unsaved change');
	});

	it('shows the saved confirmation after saving', () => {
		render(SaveBar, { props: { view: makeView({ saved: true }) } });
		expect(screen.getByTestId('save-status-saved')).toBeTruthy();
	});
});

describe('SaveBar — button state', () => {
	it('disables both buttons when clean', () => {
		render(SaveBar, { props: { view: makeView({}) } });
		expect((screen.getByTestId('discard-button') as HTMLButtonElement).disabled).toBe(true);
		expect((screen.getByTestId('save-button') as HTMLButtonElement).disabled).toBe(true);
	});

	it('enables both buttons when dirty', () => {
		render(SaveBar, { props: { view: makeView({ isDirty: true, dirtyCount: 1 }) } });
		expect((screen.getByTestId('discard-button') as HTMLButtonElement).disabled).toBe(false);
		expect((screen.getByTestId('save-button') as HTMLButtonElement).disabled).toBe(false);
	});

	it('disables both buttons while saving', () => {
		render(SaveBar, { props: { view: makeView({ isDirty: true, dirtyCount: 1, saving: true }) } });
		expect((screen.getByTestId('discard-button') as HTMLButtonElement).disabled).toBe(true);
		expect((screen.getByTestId('save-button') as HTMLButtonElement).disabled).toBe(true);
	});
});

describe('SaveBar — interactions', () => {
	it('calls view.discard() when Discard is clicked', async () => {
		const view = makeView({ isDirty: true, dirtyCount: 1 });
		render(SaveBar, { props: { view } });
		await fireEvent.click(screen.getByTestId('discard-button'));
		expect(view.discard).toHaveBeenCalledTimes(1);
	});

	it('calls view.save() when Save Changes is clicked', async () => {
		const view = makeView({ isDirty: true, dirtyCount: 1 });
		render(SaveBar, { props: { view } });
		await fireEvent.click(screen.getByTestId('save-button'));
		expect(view.save).toHaveBeenCalledTimes(1);
	});
});
