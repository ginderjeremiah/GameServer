import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

const { dangerModal } = vi.hoisted(() => ({ dangerModal: vi.fn() }));
vi.mock('$stores', () => ({ dangerModal }));

import type { BeforeNavigate } from '@sveltejs/kit';
import { confirmDiscard, createPopStateDiscardGuard, guardBeforeUnload } from '$routes/admin/discard-guard';

beforeEach(() => dangerModal.mockReset());

describe('confirmDiscard', () => {
	it('resolves true without prompting when there is nothing pending', async () => {
		await expect(confirmDiscard(0)).resolves.toBe(true);
		expect(dangerModal).not.toHaveBeenCalled();
	});

	it('prompts with singular copy for exactly one pending change', async () => {
		dangerModal.mockResolvedValue(true);
		await confirmDiscard(1);

		expect(dangerModal).toHaveBeenCalledOnce();
		const call = dangerModal.mock.calls[0][0];
		expect(call.title).toBe('Discard unsaved changes?');
		expect(call.body).toBe('You have 1 unsaved change. Leaving now will discard it.');
	});

	it('prompts with plural copy for more than one pending change', async () => {
		dangerModal.mockResolvedValue(true);
		await confirmDiscard(3);

		const call = dangerModal.mock.calls[0][0];
		expect(call.body).toBe('You have 3 unsaved changes. Leaving now will discard them.');
	});

	it('resolves to whatever the modal resolves to', async () => {
		dangerModal.mockResolvedValue(true);
		await expect(confirmDiscard(2)).resolves.toBe(true);

		dangerModal.mockResolvedValue(false);
		await expect(confirmDiscard(2)).resolves.toBe(false);
	});
});

describe('guardBeforeUnload', () => {
	// A sentinel starting value (real events default returnValue to true, not '') so an assignment
	// by the guard is observable regardless of what string it assigns.
	const fakeEvent = () =>
		({ preventDefault: vi.fn(), returnValue: true }) as unknown as BeforeUnloadEvent & {
			preventDefault: ReturnType<typeof vi.fn>;
		};

	it('does nothing when there is nothing pending', () => {
		const event = fakeEvent();
		guardBeforeUnload(event, 0);

		expect(event.preventDefault).not.toHaveBeenCalled();
		expect(event.returnValue).toBe(true);
	});

	it('prevents the default and sets returnValue when changes are pending', () => {
		const event = fakeEvent();
		guardBeforeUnload(event, 1);

		expect(event.preventDefault).toHaveBeenCalledOnce();
		expect(event.returnValue).not.toBe(true);
	});
});

describe('createPopStateDiscardGuard', () => {
	const fakeNavigation = (overrides: Partial<BeforeNavigate> = {}) =>
		({
			type: 'popstate',
			delta: -1,
			cancel: vi.fn(),
			...overrides
		}) as unknown as BeforeNavigate & { cancel: ReturnType<typeof vi.fn> };

	let historyGoSpy: ReturnType<typeof vi.spyOn>;

	beforeEach(() => {
		historyGoSpy = vi.spyOn(window.history, 'go').mockImplementation(() => {});
	});

	afterEach(() => {
		historyGoSpy.mockRestore();
	});

	it('ignores non-popstate navigations regardless of pending changes', () => {
		const guard = createPopStateDiscardGuard(() => 1);
		const navigation = fakeNavigation({ type: 'link' });

		guard(navigation);

		expect(navigation.cancel).not.toHaveBeenCalled();
		expect(dangerModal).not.toHaveBeenCalled();
	});

	it('lets a popstate navigation through when nothing is pending', () => {
		const guard = createPopStateDiscardGuard(() => 0);
		const navigation = fakeNavigation();

		guard(navigation);

		expect(navigation.cancel).not.toHaveBeenCalled();
		expect(dangerModal).not.toHaveBeenCalled();
	});

	it('cancels a dirty popstate navigation and prompts to discard', () => {
		dangerModal.mockResolvedValue(false);
		const guard = createPopStateDiscardGuard(() => 2);
		const navigation = fakeNavigation();

		guard(navigation);

		expect(navigation.cancel).toHaveBeenCalledOnce();
		expect(dangerModal).toHaveBeenCalledOnce();
	});

	it('does not replay the navigation when the prompt is declined', async () => {
		dangerModal.mockResolvedValue(false);
		const guard = createPopStateDiscardGuard(() => 2);

		guard(fakeNavigation());
		await Promise.resolve();
		await Promise.resolve();

		expect(historyGoSpy).not.toHaveBeenCalled();
	});

	it('replays the same history delta when the prompt is confirmed', async () => {
		dangerModal.mockResolvedValue(true);
		const guard = createPopStateDiscardGuard(() => 2);

		guard(fakeNavigation({ delta: -3 }));

		await vi.waitFor(() => expect(historyGoSpy).toHaveBeenCalledWith(-3));
	});

	it('lets the replayed popstate through once without re-prompting, then re-arms', async () => {
		dangerModal.mockResolvedValue(true);
		const guard = createPopStateDiscardGuard(() => 2);

		guard(fakeNavigation());
		await vi.waitFor(() => expect(historyGoSpy).toHaveBeenCalledOnce());

		dangerModal.mockClear();
		const replayed = fakeNavigation();
		guard(replayed);

		expect(replayed.cancel).not.toHaveBeenCalled();
		expect(dangerModal).not.toHaveBeenCalled();

		const another = fakeNavigation();
		guard(another);

		expect(another.cancel).toHaveBeenCalledOnce();
		expect(dangerModal).toHaveBeenCalledOnce();
	});
});
