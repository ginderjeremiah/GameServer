import { describe, it, expect } from 'vitest';
import { ESkillAcquisition, type IChallenge, type IItem, type ISkill } from '$lib/api';
import { resolveSkillProvenance } from '$routes/game/screens/codex/skill-provenance';

// Minimal fixtures — provenance only reads ids, names, the reward/grant references and the flag.
const skill = (id: number, acquisition: ESkillAcquisition): ISkill =>
	({ id, name: `Skill ${id}`, acquisition }) as ISkill;

const challenge = (id: number, rewardSkillId?: number, retiredAt?: string): IChallenge =>
	({ id, name: `Challenge ${id}`, rewardSkillId, retiredAt }) as IChallenge;

const item = (id: number, grantedSkillId?: number, retiredAt?: string): IItem =>
	({ id, name: `Item ${id}`, grantedSkillId, retiredAt }) as IItem;

describe('resolveSkillProvenance', () => {
	it('reports a challenge reward as a player source', () => {
		const result = resolveSkillProvenance(skill(5, ESkillAcquisition.Player), [challenge(0, 5), challenge(1, 9)], []);
		expect(result.status).toBe('obtainable');
		expect(result.sources).toEqual([{ kind: 'challenge', id: 0, name: 'Challenge 0' }]);
	});

	it('reports an item grant as a source', () => {
		const result = resolveSkillProvenance(skill(5, ESkillAcquisition.Item), [], [item(0, 5), item(1, 9)]);
		expect(result.status).toBe('obtainable');
		expect(result.sources).toEqual([{ kind: 'item', id: 0, name: 'Item 0' }]);
	});

	it('reports both channels, challenges before items', () => {
		const result = resolveSkillProvenance(
			skill(5, ESkillAcquisition.Player | ESkillAcquisition.Item),
			[challenge(2, 5)],
			[item(3, 5)]
		);
		expect(result.status).toBe('obtainable');
		expect(result.sources).toEqual([
			{ kind: 'challenge', id: 2, name: 'Challenge 2' },
			{ kind: 'item', id: 3, name: 'Item 3' }
		]);
	});

	it('lists every referencing challenge and item', () => {
		const result = resolveSkillProvenance(
			skill(5, ESkillAcquisition.Player | ESkillAcquisition.Item),
			[challenge(0, 5), challenge(1, 5)],
			[item(0, 5), item(1, 5)]
		);
		expect(result.sources.map((s) => `${s.kind}:${s.id}`)).toEqual(['challenge:0', 'challenge:1', 'item:0', 'item:1']);
	});

	it('treats an enemy-flagged skill with no player source as enemy-only', () => {
		const result = resolveSkillProvenance(skill(5, ESkillAcquisition.Enemy), [], []);
		expect(result.status).toBe('enemy-only');
		expect(result.sources).toEqual([]);
	});

	it('flag is intent, not reality: an Item-flagged skill with no granting item is unobtainable', () => {
		const result = resolveSkillProvenance(skill(5, ESkillAcquisition.Item), [], [item(0, 9)]);
		expect(result.status).toBe('unobtainable');
		expect(result.sources).toEqual([]);
	});

	it('still reports the real source for an enemy-flagged skill that is also rewarded', () => {
		const result = resolveSkillProvenance(
			skill(5, ESkillAcquisition.Player | ESkillAcquisition.Enemy),
			[challenge(0, 5)],
			[]
		);
		expect(result.status).toBe('obtainable');
		expect(result.sources).toEqual([{ kind: 'challenge', id: 0, name: 'Challenge 0' }]);
	});

	it('ignores retired sources filtered out by the caller', () => {
		// The caller passes only live records; a retired challenge/item simply never reaches the helper.
		const live = [item(0, 5)].filter((i) => !i.retiredAt);
		const result = resolveSkillProvenance(skill(5, ESkillAcquisition.Item), [], live);
		expect(result.sources).toEqual([{ kind: 'item', id: 0, name: 'Item 0' }]);
	});
});
