import { describe, it, expect, beforeEach, beforeAll, afterEach, vi } from 'vitest';
import { render, cleanup, screen, waitFor, fireEvent } from '@testing-library/svelte';

// jsdom has no ResizeObserver; ProgressionMap observes its container on mount.
class ResizeObserverStub {
	observe() {}
	unobserve() {}
	disconnect() {}
}

beforeAll(() => {
	(globalThis as unknown as { ResizeObserver: typeof ResizeObserverStub }).ResizeObserver = ResizeObserverStub;
});

// Mirrors progression-store.test.ts's fake in-memory backend — Progression.svelte owns its own
// ProgressionStore instance (no store prop to inject), so the socket-read layer is mocked the same
// way to drive it through a real load().
const { postMock, fetchMock, staticDataMock, toastErrorMock, referenceMock, dangerModalMock } = vi.hoisted(() => ({
	postMock: vi.fn(),
	fetchMock: vi.fn(),
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticDataMock: {} as any,
	toastErrorMock: vi.fn(),
	referenceMock: { attributeOptions: () => [] },
	dangerModalMock: vi.fn()
}));

vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, ApiRequest: { get: vi.fn(), post: postMock }, fetchSocketData: fetchMock };
});
vi.mock('$stores', () => ({ staticData: staticDataMock, toastError: toastErrorMock, dangerModal: dangerModalMock }));
vi.mock('$routes/admin/workbench/reference.svelte', () => ({ reference: referenceMock }));

import Progression from '$routes/admin/workbench/progression/Progression.svelte';
import { workbenchDirty } from '$routes/admin/workbench/dirty.svelte';

beforeEach(() => {
	postMock.mockReset();
	fetchMock.mockReset();
	fetchMock.mockResolvedValue([]);
	workbenchDirty.set(0);
});

afterEach(cleanup);

describe('Progression — unsaved-change reporting', () => {
	it("reports the store's pending-change count to the shared workbenchDirty tracker", async () => {
		render(Progression);
		await waitFor(() => expect(screen.getByTestId('progression-new-path')).toBeTruthy());
		expect(workbenchDirty.total).toBe(0);

		await fireEvent.click(screen.getByTestId('progression-new-path'));
		await waitFor(() => expect(workbenchDirty.total).toBeGreaterThan(0));
	});

	it('resets the tracker on unmount so a stale count cannot block navigation elsewhere', async () => {
		const { unmount } = render(Progression);
		await waitFor(() => expect(screen.getByTestId('progression-new-path')).toBeTruthy());
		await fireEvent.click(screen.getByTestId('progression-new-path'));
		await waitFor(() => expect(workbenchDirty.total).toBeGreaterThan(0));

		unmount();
		expect(workbenchDirty.total).toBe(0);
	});
});

describe('Progression — load-failure recovery', () => {
	it('shows a persistent error panel with a retry affordance instead of spinning forever, and recovers on retry', async () => {
		fetchMock.mockRejectedValueOnce(new Error('network down'));
		fetchMock.mockResolvedValue([]);
		render(Progression);

		await waitFor(() => expect(screen.getByTestId('progression-error')).toBeTruthy());
		expect(screen.getByTestId('progression-error').textContent).toContain('network down');
		expect(screen.queryByTestId('progression-new-path')).toBeNull();

		await fireEvent.click(screen.getByText('Refresh'));

		await waitFor(() => expect(screen.getByTestId('progression-new-path')).toBeTruthy());
		expect(screen.queryByTestId('progression-error')).toBeNull();
	});
});

describe('Progression — Map view save bar', () => {
	it('keeps the save bar (and its unsaved-change count) visible after switching to Map view', async () => {
		render(Progression);
		await waitFor(() => expect(screen.getByTestId('progression-new-path')).toBeTruthy());

		await fireEvent.click(screen.getByTestId('progression-new-path'));
		await waitFor(() => expect(workbenchDirty.total).toBeGreaterThan(0));

		await fireEvent.click(screen.getByTestId('progression-map-toggle'));
		await waitFor(() => expect(screen.getByTestId('progression-map')).toBeTruthy());

		expect(screen.getByTestId('progression-save')).toBeTruthy();
	});
});
