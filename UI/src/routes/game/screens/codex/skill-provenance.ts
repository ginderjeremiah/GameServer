/* Skill acquisition/provenance — how a skill enters the player's hands, derived from *actual
   references* (items whose `grantedSkillId` is the skill), with the `ESkillAcquisition` flag deciding
   *only* the no-source wording. Intent (the flag) never invents a source: an `Item`-flagged skill
   that no item grants is not reported as item-obtainable. Challenges no longer grant skills (spike
   #982), so item grants are the only concrete player source surfaced here; proficiency-milestone
   grants are shown on the proficiency tree, not the codex. Pure + store-free so it can be
   unit-tested without rendering. */

import { ESkillAcquisition, type IItem, type ISkill } from '$lib/api';

/** A concrete reference that grants a skill to the player: an item that grants it while equipped. */
export interface SkillSource {
	kind: 'item';
	id: number;
	name: string;
}

/** How (if at all) a skill can enter the player's hands:
 *  - `obtainable` — at least one concrete source (an item grant) exists.
 *  - `enemy-only` — no player source, but the skill is `Enemy`-flagged (encountered, never earned).
 *  - `unobtainable` — no player source and not enemy-flagged (e.g. flagged but unreferenced). */
export type SkillAcquisitionStatus = 'obtainable' | 'enemy-only' | 'unobtainable';

export interface SkillProvenance {
	status: SkillAcquisitionStatus;
	/** Concrete sources — the items that grant the skill, in the order given. */
	sources: SkillSource[];
}

/** Derive a skill's provenance from the (already live/non-retired) items that grant it, using the
 *  acquisition flag only to word the no-source case. */
export function resolveSkillProvenance(skill: ISkill, items: IItem[]): SkillProvenance {
	const sources: SkillSource[] = items
		.filter((i) => i.grantedSkillId === skill.id)
		.map((i) => ({ kind: 'item' as const, id: i.id, name: i.name }));
	if (sources.length > 0) {
		return { status: 'obtainable', sources };
	}
	const enemyOnly = (skill.acquisition & ESkillAcquisition.Enemy) !== 0;
	return { status: enemyOnly ? 'enemy-only' : 'unobtainable', sources };
}
