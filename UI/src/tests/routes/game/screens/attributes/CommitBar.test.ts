import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import type { AttributesView } from '$routes/game/screens/attributes/attributes-view.svelte';

import CommitBar from '$routes/game/screens/attributes/CommitBar.svelte';

afterEach(cleanup);

const makeView = (
	overrides: Partial<{ saved: boolean; dirty: boolean; saving: boolean; changedCount: number }>
): AttributesView =>
	({
		saved: false,
		dirty: false,
		saving: false,
		changedCount: 0,
		discard: vi.fn(),
		save: vi.fn(),
		...overrides
	}) as unknown as AttributesView;

describe('CommitBar — status text', () => {
	it('shows "No changes" when the allocation is clean', () => {
		render(CommitBar, { props: { view: makeView({}) } });
		expect(screen.getByText('No changes')).toBeTruthy();
	});

	it('shows the changed count for multiple attributes', () => {
		render(CommitBar, { props: { view: makeView({ dirty: true, changedCount: 2 }) } });
		expect(screen.getByText('2 attributes changed')).toBeTruthy();
	});

	it('uses singular "attribute" when exactly 1 attribute changed', () => {
		render(CommitBar, { props: { view: makeView({ dirty: true, changedCount: 1 }) } });
		expect(screen.getByText('1 attribute changed')).toBeTruthy();
	});

	it('shows "Attributes saved" after a successful save', () => {
		render(CommitBar, { props: { view: makeView({ saved: true }) } });
		expect(screen.getByText('Attributes saved')).toBeTruthy();
	});
});

describe('CommitBar — button state', () => {
	it('disables both action buttons when clean', () => {
		render(CommitBar, { props: { view: makeView({}) } });
		const buttons = screen.getAllByRole('button') as HTMLButtonElement[];
		expect(buttons.every((b) => b.disabled)).toBe(true);
	});

	it('enables both action buttons when dirty', () => {
		render(CommitBar, { props: { view: makeView({ dirty: true, changedCount: 1 }) } });
		const buttons = screen.getAllByRole('button') as HTMLButtonElement[];
		expect(buttons.every((b) => !b.disabled)).toBe(true);
	});

	it('disables both buttons while saving', () => {
		render(CommitBar, { props: { view: makeView({ dirty: true, saving: true, changedCount: 1 }) } });
		const buttons = screen.getAllByRole('button') as HTMLButtonElement[];
		expect(buttons.every((b) => b.disabled)).toBe(true);
	});

	it('shows "Saving…" on the confirm button while saving', () => {
		render(CommitBar, { props: { view: makeView({ dirty: true, saving: true, changedCount: 1 }) } });
		expect(screen.getByText('Saving…')).toBeTruthy();
	});
});

describe('CommitBar — interactions', () => {
	it('calls view.discard() when Discard is clicked', async () => {
		const view = makeView({ dirty: true, changedCount: 1 });
		render(CommitBar, { props: { view } });
		await fireEvent.click(screen.getByText('Discard'));
		expect(view.discard).toHaveBeenCalledTimes(1);
	});

	it('calls view.save() when Confirm is clicked', async () => {
		const view = makeView({ dirty: true, changedCount: 1 });
		render(CommitBar, { props: { view } });
		await fireEvent.click(screen.getByText('Confirm'));
		expect(view.save).toHaveBeenCalledTimes(1);
	});
});
