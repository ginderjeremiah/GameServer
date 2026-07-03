import { describe, it, expect, beforeEach, vi } from 'vitest';

const { dangerModal } = vi.hoisted(() => ({ dangerModal: vi.fn() }));
vi.mock('$stores', () => ({ dangerModal }));

import { confirmDiscard, guardBeforeUnload } from '$routes/admin/discard-guard';

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
