import { describe, it, expect, vi, beforeEach } from 'vitest';

const { get, confirmModal } = vi.hoisted(() => ({
	get: vi.fn(),
	confirmModal: vi.fn()
}));

vi.mock('$lib/api', () => ({
	ApiRequest: class {
		get = get;
		constructor() {}
	}
}));
vi.mock('$stores', () => ({ confirmModal }));

import { confirmSessionTakeover } from '../../../routes/login/session-takeover';

describe('confirmSessionTakeover', () => {
	beforeEach(() => {
		get.mockReset();
		confirmModal.mockReset();
	});

	it('checks presence for the given player and proceeds without prompting when no other session is active', async () => {
		get.mockResolvedValue({ status: 200, data: { active: false } });

		const result = await confirmSessionTakeover(1);

		expect(get).toHaveBeenCalledWith({ playerId: 1 });
		expect(result).toBe(true);
		expect(confirmModal).not.toHaveBeenCalled();
	});

	it('fails open and proceeds when the check returns a non-200 response', async () => {
		get.mockResolvedValue({ status: 0 });

		const result = await confirmSessionTakeover(1);

		expect(result).toBe(true);
		expect(confirmModal).not.toHaveBeenCalled();
	});

	it('proceeds when the user confirms the takeover', async () => {
		get.mockResolvedValue({ status: 200, data: { active: true } });
		confirmModal.mockResolvedValue(true);

		const result = await confirmSessionTakeover(1);

		expect(result).toBe(true);
		expect(confirmModal).toHaveBeenCalledOnce();
	});

	it('does not proceed when the user declines the takeover, with no other side effect', async () => {
		get.mockResolvedValue({ status: 200, data: { active: true } });
		confirmModal.mockResolvedValue(false);

		const result = await confirmSessionTakeover(1);

		expect(result).toBe(false);
		expect(confirmModal).toHaveBeenCalledOnce();
	});
});
