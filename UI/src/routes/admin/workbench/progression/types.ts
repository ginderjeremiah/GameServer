import type { IPath, IProficiency } from '$lib/api';

/**
 * A path as edited in the workbench. Identical to the {@link IPath} contract — its ordered tiers
 * are the proficiencies carrying its id (kept in a sibling catalogue), and its skill contributions
 * ride along on `contributions` (persisted through the dedicated SetPathContributions endpoint).
 */
export type WorkbenchPath = IPath;

/**
 * A proficiency (a path tier) as edited in the workbench. The optional seed-skill FK is widened to a
 * plain number so the select can use a "None" sentinel (-1), mirroring how items/classes handle their
 * optional granted/scaling FKs; it maps back to `undefined` in the persisted DTO.
 */
export interface WorkbenchProficiency extends Omit<IProficiency, 'seedSkillId'> {
	seedSkillId: number;
}

/** The "no seed skill" sentinel for the seed-skill select. */
export const NO_SEED_SKILL = -1;

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
