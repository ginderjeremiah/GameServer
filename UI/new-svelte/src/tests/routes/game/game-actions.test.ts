import { describe, it, expect, vi, beforeEach } from 'vitest';

const { confirmModal, logout } = vi.hoisted(() => ({
	confirmModal: vi.fn<() => Promise<boolean>>(),
	logout: vi.fn()
}));

vi.mock('$stores', () => ({ confirmModal }));
vi.mock('$lib/api', () => ({ logout }));

import { confirmQuit } from '$routes/game/game-actions';

beforeEach(() => {
	vi.clearAllMocks();
});

describe('confirmQuit', () => {
	it('opens a confirm modal with the correct title, body, and button labels', async () => {
		confirmModal.mockResolvedValue(false);

		await confirmQuit();

		expect(confirmModal).toHaveBeenCalledWith({
			title: 'Log out?',
			body: "You'll be signed out and returned to the login screen.",
			confirmLabel: 'Log out',
			cancelLabel: 'Stay'
		});
	});

	it('calls logout when the user confirms', async () => {
		confirmModal.mockResolvedValue(true);

		await confirmQuit();

		expect(logout).toHaveBeenCalledTimes(1);
	});

	it('does not call logout when the user cancels', async () => {
		confirmModal.mockResolvedValue(false);

		await confirmQuit();

		expect(logout).not.toHaveBeenCalled();
	});
});
