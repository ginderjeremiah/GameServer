import { describe, it, expect, beforeEach, vi } from 'vitest';

const { goto, hasRole, toastError } = vi.hoisted(() => ({
	goto: vi.fn(() => Promise.resolve()),
	hasRole: vi.fn<(role: string) => boolean>(),
	toastError: vi.fn()
}));

vi.mock('$app/navigation', () => ({ goto }));
vi.mock('$app/paths', () => ({ resolve: (p: string) => p }));
vi.mock('$lib/api', () => ({ ERole: { Admin: 'Admin' }, hasRole }));
vi.mock('$stores', () => ({ toastError }));

import { ensureAdminAccess } from '$routes/admin/admin-access';

describe('ensureAdminAccess', () => {
	beforeEach(() => {
		vi.clearAllMocks();
	});

	it('grants an Admin access without redirecting or toasting', () => {
		hasRole.mockReturnValue(true);

		expect(ensureAdminAccess()).toBe(true);
		expect(toastError).not.toHaveBeenCalled();
		expect(goto).not.toHaveBeenCalled();
	});

	it('denies a non-admin: surfaces an access-denied toast and returns to login', () => {
		hasRole.mockReturnValue(false);

		expect(ensureAdminAccess()).toBe(false);
		expect(toastError).toHaveBeenCalledTimes(1);
		expect(goto).toHaveBeenCalledWith('/');
	});

	it('gates specifically on the Admin role', () => {
		hasRole.mockReturnValue(false);

		ensureAdminAccess();

		expect(hasRole).toHaveBeenCalledWith('Admin');
	});
});
