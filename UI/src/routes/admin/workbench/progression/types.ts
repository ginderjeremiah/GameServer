import type { IPath, IProficiency } from '$lib/api';

/**
 * A path as edited in the workbench. Identical to the {@link IPath} contract — its ordered tiers
 * are the proficiencies carrying its id (kept in a sibling catalogue), and it declares the single
 * activity key it trains on.
 */
export type WorkbenchPath = IPath;

/**
 * A proficiency (a path tier) as edited in the workbench. Identical to the {@link IProficiency} contract —
 * its level modifiers/rewards and prerequisites ride along and are persisted through their dedicated
 * relationship endpoints.
 */
export type WorkbenchProficiency = IProficiency;

/** The "no skill" sentinel for the milestone reward-skill select (-1 ⇒ no skill chosen / cleared). */
export const NO_SKILL = -1;

/** A detail-pane tab descriptor (shared header chrome). */
export interface ProgressionTab {
	key: string;
	label: string;
	count?: number | null;
	dirty?: boolean;
	warn?: boolean;
	disabled?: boolean;
}
