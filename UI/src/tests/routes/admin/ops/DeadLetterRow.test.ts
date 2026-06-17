import { describe, it, expect, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import DeadLetterRow from '$routes/admin/ops/DeadLetterRow.svelte';
import { EDeadLetterReason, type IDeadLetterEntry } from '$lib/api';

afterEach(cleanup);

const entry = (overrides: Partial<IDeadLetterEntry> = {}): IDeadLetterEntry => ({
	index: 3,
	eventType: 'PlayerLeveledEvent',
	playerId: 42,
	reason: EDeadLetterReason.Replayable,
	rawPayload: '{"playerId":42,"level":5}',
	...overrides
});

// DeadLetterRow renders <tr> roots; wrap it in a table so the markup is valid in jsdom.
const renderRow = (props: { entry: IDeadLetterEntry; selected: boolean; onToggle: () => void }) => {
	const table = document.createElement('table');
	const tbody = document.createElement('tbody');
	table.appendChild(tbody);
	document.body.appendChild(table);
	return render(DeadLetterRow, { props: { ...props, columns: 6 }, target: tbody });
};

describe('DeadLetterRow', () => {
	it('renders the index, event type, and player', () => {
		renderRow({ entry: entry(), selected: false, onToggle: vi.fn() });
		expect(screen.getByText('3')).toBeTruthy();
		expect(screen.getByText('PlayerLeveledEvent')).toBeTruthy();
		expect(screen.getByText('42')).toBeTruthy();
	});

	it('shows an em dash for a missing event type and player', () => {
		renderRow({
			entry: entry({ eventType: undefined, playerId: undefined }),
			selected: false,
			onToggle: vi.fn()
		});
		expect(screen.getAllByText('—').length).toBe(2);
	});

	it('reflects the selected state on the checkbox and fires onToggle', async () => {
		const onToggle = vi.fn();
		renderRow({ entry: entry(), selected: true, onToggle });
		const check = screen.getByTestId('dl-row-check') as HTMLInputElement;
		expect(check.checked).toBe(true);

		await fireEvent.click(check);
		expect(onToggle).toHaveBeenCalledOnce();
	});

	it('expands to reveal the pretty-printed payload and collapses again', async () => {
		renderRow({ entry: entry(), selected: false, onToggle: vi.fn() });
		expect(screen.queryByTestId('dl-row-payload')).toBeNull();

		await fireEvent.click(screen.getByTestId('dl-row-expand'));
		const payload = screen.getByTestId('dl-row-payload');
		expect(payload.textContent).toContain('"playerId": 42');

		await fireEvent.click(screen.getByTestId('dl-row-expand'));
		expect(screen.queryByTestId('dl-row-payload')).toBeNull();
	});
});
