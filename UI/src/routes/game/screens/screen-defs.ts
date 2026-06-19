import type { Component } from 'svelte';
import { ERole } from '$lib/api';
import Fight from './fight/Fight.svelte';
import CardGame from './card-game/CardGame.svelte';
import Challenges from './challenges/Challenges.svelte';
import Inventory from './inventory/Inventory.svelte';
import Skills from './skills/Skills.svelte';
import Attributes from './attributes/Attributes.svelte';
import AttributeBreakdown from './attribute-breakdown/AttributeBreakdown.svelte';
import Statistics from './stats/Statistics.svelte';
import Options from './options/Options.svelte';

/** A navigable game screen shown in the sidebar. */
export interface ScreenDef {
	key: string;
	label: string;
	group: string;
	/** Whether the screen is implemented (vs. a "wip" placeholder). */
	built: boolean;
	/**
	 * The component rendered when this screen is active — the single source of truth for the screen's
	 * UI. Omitted for entries that render nothing of their own: a "wip" placeholder (`built: false`,
	 * which falls back to {@link PlaceholderScreen}) or an action-only entry that navigates or opens a
	 * dialog instead (e.g. Admin, Quit).
	 */
	component?: Component;
	/** When set, the screen is only available to users holding this role. */
	requiresRole?: ERole;
}

/**
 * The full set of game screens. Adding a screen means adding a single entry here — its `component`
 * is rendered directly, so there is no separate key→component map to keep in sync. Role-gated
 * screens (e.g. Admin) are filtered out for users without the role by {@link visibleScreens} before
 * the list reaches the sidebar.
 */
export const GAME_SCREENS: ScreenDef[] = [
	{ key: 'fight', label: 'Fight', group: 'combat', built: true, component: Fight },
	{ key: 'cardGame', label: 'Card Game', group: 'combat', built: true, component: CardGame },
	{ key: 'challenges', label: 'Challenges', group: 'combat', built: true, component: Challenges },
	{ key: 'inventory', label: 'Inventory', group: 'character', built: true, component: Inventory },
	{ key: 'skills', label: 'Skills', group: 'character', built: true, component: Skills },
	{ key: 'attributes', label: 'Attributes', group: 'character', built: true, component: Attributes },
	{
		key: 'attributeBreakdown',
		label: 'Attribute Breakdown',
		group: 'character',
		built: true,
		component: AttributeBreakdown
	},
	{ key: 'stats', label: 'Stats', group: 'character', built: true, component: Statistics },
	{ key: 'options', label: 'Options', group: 'settings', built: true, component: Options },
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
