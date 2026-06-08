import { describe, it, expect } from 'vitest';
import { GAME_SCREENS, visibleScreens, type ScreenDef } from '$routes/game/screens/screen-defs';
import { ERole } from '$lib/api';

const screens: ScreenDef[] = [
	{ key: 'fight', label: 'Fight', group: 'combat', built: true },
	{ key: 'options', label: 'Options', group: 'settings', built: true },
	{ key: 'admin', label: 'Admin', group: 'admin', built: true, requiresRole: ERole.Admin }
];

describe('visibleScreens', () => {
	it('keeps all non-role-gated screens regardless of roles', () => {
		const keys = visibleScreens(screens, []).map((s) => s.key);

		expect(keys).toContain('fight');
		expect(keys).toContain('options');
	});

	it('hides a role-gated screen when the user lacks the role', () => {
		const keys = visibleScreens(screens, []).map((s) => s.key);

		expect(keys).not.toContain('admin');
	});

	it('hides a role-gated screen when the user has only unrelated roles', () => {
		const keys = visibleScreens(screens, ['Moderator']).map((s) => s.key);

		expect(keys).not.toContain('admin');
	});

	it('shows a role-gated screen when the user holds the role', () => {
		const keys = visibleScreens(screens, [ERole.Admin]).map((s) => s.key);

		expect(keys).toContain('admin');
	});
});

describe('GAME_SCREENS', () => {
	it('gates the Admin screen behind the Admin role', () => {
		const admin = GAME_SCREENS.find((s) => s.key === 'admin');

		expect(admin?.requiresRole).toBe(ERole.Admin);
	});

	it('leaves the ordinary screens ungated', () => {
		const gated = GAME_SCREENS.filter((s) => s.requiresRole).map((s) => s.key);

		expect(gated).toEqual(['admin']);
	});
});
