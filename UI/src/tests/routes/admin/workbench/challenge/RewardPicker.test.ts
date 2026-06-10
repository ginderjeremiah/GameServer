import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import type { IChallenge } from '$lib/api';

import RewardPicker, { type PickerRecord } from '$routes/admin/workbench/components/challenge/RewardPicker.svelte';

const RECORDS: PickerRecord[] = [
	{ id: 1, name: 'Iron Helm', color: 'var(--rarity-common)', tag: 'Common' },
	{ id: 2, name: 'Dragon Blade', color: 'var(--rarity-legendary)', tag: 'Legendary' },
	{ id: 3, name: 'Leather Boots', color: 'var(--rarity-common)', tag: 'Common' }
];

const claimedBy = (id: number, name: string): Map<number, IChallenge> =>
	new Map([[id, { id: 99, name } as IChallenge]]);

afterEach(cleanup);

describe('RewardPicker', () => {
	it('renders a row per record', () => {
		const { container } = render(RewardPicker, {
			props: {
				kind: 'item',
				records: RECORDS,
				currentId: undefined,
				claimed: new Map(),
				onPick: vi.fn(),
				onClose: vi.fn()
			}
		});
		expect(container.querySelectorAll('.ch-picker-row')).toHaveLength(3);
	});

	it('reports the unclaimed-of-total count', () => {
		render(RewardPicker, {
			props: {
				kind: 'item',
				records: RECORDS,
				currentId: undefined,
				claimed: claimedBy(2, 'Boss Slayer'),
				onPick: vi.fn(),
				onClose: vi.fn()
			}
		});
		expect(screen.getByText('2 of 3 unclaimed')).toBeTruthy();
	});

	it('filters records by the search query', async () => {
		const { container } = render(RewardPicker, {
			props: {
				kind: 'item',
				records: RECORDS,
				currentId: undefined,
				claimed: new Map(),
				onPick: vi.fn(),
				onClose: vi.fn()
			}
		});
		await fireEvent.input(container.querySelector('input.inp') as HTMLInputElement, { target: { value: 'blade' } });
		expect(container.querySelectorAll('.ch-picker-row')).toHaveLength(1);
		expect(screen.getByText('Dragon Blade')).toBeTruthy();
	});

	it('shows "No matches." when the query matches nothing', async () => {
		const { container } = render(RewardPicker, {
			props: {
				kind: 'item',
				records: RECORDS,
				currentId: undefined,
				claimed: new Map(),
				onPick: vi.fn(),
				onClose: vi.fn()
			}
		});
		await fireEvent.input(container.querySelector('input.inp') as HTMLInputElement, { target: { value: 'zzz' } });
		expect(screen.getByText('No matches.')).toBeTruthy();
	});

	it('disables a claimed record and surfaces the owning challenge', () => {
		const { container } = render(RewardPicker, {
			props: {
				kind: 'item',
				records: RECORDS,
				currentId: undefined,
				claimed: claimedBy(2, 'Boss Slayer'),
				onPick: vi.fn(),
				onClose: vi.fn()
			}
		});
		const claimedRow = container.querySelector('.ch-picker-row.claimed') as HTMLButtonElement;
		expect(claimedRow.disabled).toBe(true);
		expect(claimedRow.textContent).toContain('Boss Slayer');
	});

	it('does not pick a claimed record', async () => {
		const onPick = vi.fn();
		render(RewardPicker, {
			props: {
				kind: 'item',
				records: RECORDS,
				currentId: undefined,
				claimed: claimedBy(2, 'Boss Slayer'),
				onPick,
				onClose: vi.fn()
			}
		});
		await fireEvent.click(screen.getByText('Dragon Blade'));
		expect(onPick).not.toHaveBeenCalled();
	});

	it('marks the current selection', () => {
		render(RewardPicker, {
			props: { kind: 'item', records: RECORDS, currentId: 1, claimed: new Map(), onPick: vi.fn(), onClose: vi.fn() }
		});
		expect(screen.getByText('selected')).toBeTruthy();
	});

	it('picks an unclaimed record on click', async () => {
		const onPick = vi.fn();
		render(RewardPicker, {
			props: { kind: 'item', records: RECORDS, currentId: undefined, claimed: new Map(), onPick, onClose: vi.fn() }
		});
		await fireEvent.click(screen.getByText('Iron Helm'));
		expect(onPick).toHaveBeenCalledWith(1);
	});

	it('closes via the close control', async () => {
		const onClose = vi.fn();
		const { container } = render(RewardPicker, {
			props: { kind: 'item', records: RECORDS, currentId: undefined, claimed: new Map(), onPick: vi.fn(), onClose }
		});
		await fireEvent.click(container.querySelector('.row-x') as HTMLElement);
		expect(onClose).toHaveBeenCalledOnce();
	});
});
