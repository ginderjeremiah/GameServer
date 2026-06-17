import { describe, it, expect, afterEach, beforeEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent, waitFor } from '@testing-library/svelte';
import type { IDeadLetterEntry, IDeadLetterInspection } from '$lib/api';

const { getMock, postMock, confirmModalMock, toastSuccessMock, toastErrorMock } = vi.hoisted(() => ({
	getMock: vi.fn(),
	postMock: vi.fn(),
	confirmModalMock: vi.fn(),
	toastSuccessMock: vi.fn(),
	toastErrorMock: vi.fn()
}));

vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, ApiRequest: { get: getMock, post: postMock } };
});
vi.mock('$stores', () => ({
	confirmModal: confirmModalMock,
	toastSuccess: toastSuccessMock,
	toastError: toastErrorMock
}));

import DeadLetterConsole from '$routes/admin/ops/DeadLetterConsole.svelte';
import { EDeadLetterReason } from '$lib/api';

const entry = (overrides: Partial<IDeadLetterEntry> & Pick<IDeadLetterEntry, 'index'>): IDeadLetterEntry => ({
	eventType: 'PlayerLeveledEvent',
	playerId: 7,
	reason: EDeadLetterReason.Replayable,
	rawPayload: `{"index":${overrides.index}}`,
	...overrides
});

const inspection = (entries: IDeadLetterEntry[], total = entries.length): IDeadLetterInspection => ({
	totalCount: total,
	entries
});

beforeEach(() => {
	getMock.mockReset();
	postMock.mockReset();
	confirmModalMock.mockReset();
	toastSuccessMock.mockReset();
	toastErrorMock.mockReset();
});

afterEach(cleanup);

describe('DeadLetterConsole', () => {
	it('loads on mount and renders the queue depth and one row per entry', async () => {
		getMock.mockResolvedValue(inspection([entry({ index: 0 }), entry({ index: 1 })], 2));
		render(DeadLetterConsole);

		await waitFor(() => expect(screen.getByTestId('dl-table')).toBeTruthy());
		expect(screen.getByTestId('dl-depth').textContent).toBe('2 in queue');
		expect(screen.getAllByTestId('dl-row').length).toBe(2);
	});

	it('shows the empty state when the queue is empty', async () => {
		getMock.mockResolvedValue(inspection([], 0));
		render(DeadLetterConsole);

		await waitFor(() => expect(screen.getByTestId('dl-empty')).toBeTruthy());
		expect(screen.queryByTestId('dl-table')).toBeNull();
		// Replay all is disabled with nothing queued.
		expect((screen.getByTestId('dl-replay-all') as HTMLButtonElement).disabled).toBe(true);
	});

	it('toasts the error and shows the error panel when the load fails', async () => {
		getMock.mockRejectedValue(new Error('queue unreachable'));
		render(DeadLetterConsole);

		await waitFor(() => expect(toastErrorMock).toHaveBeenCalledWith('queue unreachable'));
		expect(screen.getByTestId('dl-error').textContent).toBe('queue unreachable');
	});

	it('disables replay-selected until entries are selected', async () => {
		getMock.mockResolvedValue(inspection([entry({ index: 0 })]));
		render(DeadLetterConsole);

		await waitFor(() => expect(screen.getByTestId('dl-table')).toBeTruthy());
		expect((screen.getByTestId('dl-replay-selected') as HTMLButtonElement).disabled).toBe(true);

		await fireEvent.click(screen.getByTestId('dl-select-all'));
		expect((screen.getByTestId('dl-replay-selected') as HTMLButtonElement).disabled).toBe(false);
	});

	it('replays all after confirmation and toasts the result', async () => {
		getMock.mockResolvedValue(inspection([entry({ index: 0 })], 1));
		postMock.mockResolvedValue({ replayedCount: 1, remainingCount: 0 });
		confirmModalMock.mockResolvedValue(true);
		render(DeadLetterConsole);

		await waitFor(() => expect(screen.getByTestId('dl-table')).toBeTruthy());
		await fireEvent.click(screen.getByTestId('dl-replay-all'));

		// toastSuccess is the final step (after post + reload), so waiting on it implies the post landed.
		await waitFor(() => expect(toastSuccessMock).toHaveBeenCalledWith('Replayed 1 entry; 0 remaining.'));
		expect(postMock).toHaveBeenCalledWith('AdminTools/ReplayPlayerUpdateDeadLetters', {
			all: true,
			payloads: undefined
		});
	});

	it('does not replay when the confirmation is dismissed', async () => {
		getMock.mockResolvedValue(inspection([entry({ index: 0 })], 1));
		confirmModalMock.mockResolvedValue(false);
		render(DeadLetterConsole);

		await waitFor(() => expect(screen.getByTestId('dl-table')).toBeTruthy());
		await fireEvent.click(screen.getByTestId('dl-replay-all'));

		await waitFor(() => expect(confirmModalMock).toHaveBeenCalled());
		expect(postMock).not.toHaveBeenCalled();
	});

	it('replays the selected entries by payload', async () => {
		getMock.mockResolvedValue(
			inspection([entry({ index: 0, rawPayload: 'p0' }), entry({ index: 1, rawPayload: 'p1' })])
		);
		postMock.mockResolvedValue({ replayedCount: 1, remainingCount: 1 });
		confirmModalMock.mockResolvedValue(true);
		render(DeadLetterConsole);

		await waitFor(() => expect(screen.getByTestId('dl-table')).toBeTruthy());
		const checks = screen.getAllByTestId('dl-row-check');
		await fireEvent.click(checks[0]);
		await fireEvent.click(screen.getByTestId('dl-replay-selected'));

		await waitFor(() =>
			expect(postMock).toHaveBeenCalledWith('AdminTools/ReplayPlayerUpdateDeadLetters', {
				all: false,
				payloads: ['p0']
			})
		);
	});
});
