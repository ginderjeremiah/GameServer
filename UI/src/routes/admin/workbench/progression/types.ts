import type { IPath, IProficiency } from '$lib/api';

/**
 * A path as edited in the workbench. Identical to the {@link IPath} contract — its ordered tiers
 * are the proficiencies carrying its id (kept in a sibling catalogue), and its skill contributions
 * ride along on `contributions` (persisted through the dedicated SetPathContributions endpoint).
 */
export type WorkbenchPath = IPath;

/** A proficiency (a path tier) as edited in the workbench. Identical to the {@link IProficiency} contract. */
export type WorkbenchProficiency = IProficiency;

/** The "no skill" sentinel for the milestone reward-skill select (maps back to no reward). */
export const NO_SKILL = -1;

/** The two record kinds the progression editor authors. */
export type ProgressionKind = 'path' | 'tier';

/** A detail-pane tab descriptor (shared header chrome). */
export interface ProgressionTab {
	key: string;
	label: string;
	count?: number | null;
	dirty?: boolean;
	warn?: boolean;
	disabled?: boolean;
}
