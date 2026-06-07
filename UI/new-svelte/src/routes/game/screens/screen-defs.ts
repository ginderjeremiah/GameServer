import { ERole } from '$lib/api';

/** A navigable game screen shown in the sidebar. */
export interface ScreenDef {
	key: string;
	label: string;
	group: string;
	/** Whether the screen is implemented (vs. a "wip" placeholder). */
	built: boolean;
	/** When set, the screen is only available to users holding this role. */
	requiresRole?: ERole;
}

/**
 * The full set of game screens. Role-gated screens (e.g. Admin) are filtered out for users without
 * the role by {@link visibleScreens} before the list reaches the sidebar.
 */
export const GAME_SCREENS: ScreenDef[] = [
	{ key: 'fight', label: 'Fight', group: 'combat', built: true },
	{ key: 'cardGame', label: 'Card Game', group: 'combat', built: false },
	{ key: 'challenges', label: 'Challenges', group: 'combat', built: true },
	{ key: 'inventory', label: 'Inventory', group: 'character', built: true },
	{ key: 'attributes', label: 'Attributes', group: 'character', built: true },
	{ key: 'attributeBreakdown', label: 'Attribute Breakdown', group: 'character', built: true },
	{ key: 'stats', label: 'Stats', group: 'character', built: true },
	{ key: 'options', label: 'Options', group: 'settings', built: true },
	{ key: 'help', label: 'Help', group: 'settings', built: false },
	{ key: 'quit', label: 'Quit', group: 'settings', built: true },
	{ key: 'admin', label: 'Admin', group: 'admin', built: true, requiresRole: ERole.Admin }
];

/**
 * Filters the screen list down to those a user with the given roles may see: a screen with no
 * `requiresRole` is always visible, and a role-gated one only when its role is present. Pure so the
 * gating rule can be unit-tested independently of the sidebar.
 */
export const visibleScreens = (screens: ScreenDef[], roles: readonly string[]): ScreenDef[] =>
	screens.filter((s) => !s.requiresRole || roles.includes(s.requiresRole));
