import { describe, it, expect, beforeEach, vi } from 'vitest';

const { getRoles } = vi.hoisted(() => ({ getRoles: vi.fn<() => string[]>() }));
vi.mock('$lib/api/token-store', () => ({ getRoles }));

import { ERole, hasRole } from '$lib/api/roles';

describe('roles', () => {
	beforeEach(() => {
		vi.clearAllMocks();
	});

	it('reports false when the user holds no roles', () => {
		getRoles.mockReturnValue([]);

		expect(hasRole(ERole.Admin)).toBe(false);
	});

	it('reports true when the user holds the requested role', () => {
		getRoles.mockReturnValue(['Admin']);

		expect(hasRole(ERole.Admin)).toBe(true);
	});

	it('reports false when the user holds other roles but not the requested one', () => {
		getRoles.mockReturnValue(['Moderator']);

		expect(hasRole(ERole.Admin)).toBe(false);
	});
});
