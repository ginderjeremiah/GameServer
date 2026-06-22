/* Skill acquisition/provenance — how a skill enters the player's hands, derived from *actual
   references* (challenges whose `rewardSkillId` is the skill, items whose `grantedSkillId` is the
   skill), with the `ESkillAcquisition` flag deciding *only* the no-source wording. Intent (the flag)
   never invents a source: an `Item`-flagged skill that no item grants is not reported as
   item-obtainable. Pure + store-free so it can be unit-tested without rendering. */

import { ESkillAcquisition, type IChallenge, type IItem, type ISkill } from '$lib/api';

/** A concrete reference that grants a skill to the player: a challenge that rewards it on completion,
 *  or an item that grants it while equipped. */
export interface SkillSource {
	kind: 'challenge' | 'item';
	id: number;
	name: string;
}

/** How (if at all) a skill can enter the player's hands:
 *  - `obtainable` — at least one concrete source (challenge reward and/or item grant) exists.
 *  - `enemy-only` — no player source, but the skill is `Enemy`-flagged (encountered, never earned).
 *  - `unobtainable` — no player source and not enemy-flagged (e.g. flagged but unreferenced). */
export type SkillAcquisitionStatus = 'obtainable' | 'enemy-only' | 'unobtainable';

export interface SkillProvenance {
	status: SkillAcquisitionStatus;
	/** Concrete sources — challenge rewards first (in the order given), then item grants. */
	sources: SkillSource[];
}

/** Derive a skill's provenance from the (already live/non-retired) challenges and items that
 *  reference it, using the acquisition flag only to word the no-source case. */
export function resolveSkillProvenance(skill: ISkill, challenges: IChallenge[], items: IItem[]): SkillProvenance {
	const sources: SkillSource[] = [
		...challenges
			.filter((c) => c.rewardSkillId === skill.id)
			.map((c) => ({ kind: 'challenge' as const, id: c.id, name: c.name })),
		...items
			.filter((i) => i.grantedSkillId === skill.id)
			.map((i) => ({ kind: 'item' as const, id: i.id, name: i.name }))
	];
	if (sources.length > 0) {
		return { status: 'obtainable', sources };
	}
	const enemyOnly = (skill.acquisition & ESkillAcquisition.Enemy) !== 0;
	return { status: enemyOnly ? 'enemy-only' : 'unobtainable', sources };
}
