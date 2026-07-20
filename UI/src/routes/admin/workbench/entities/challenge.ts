import { ApiRequest, EChallengeType, EEntityType, fetchSocketData, type IChallenge } from '$lib/api';
import { staticData } from '$stores';
import { reference } from '../reference.svelte';
import { persistEntity } from '../save-helpers';
import { challengeSentence, deriveFromType, fmtNum, typeBossOnly } from './challenge-helpers';
import type { EntityConfig } from './types';

// Challenges load over the socket; the admin filter invalidates this cache on every
// write server-side, so a plain refetch returns the freshly-saved list.
const refresh = async (): Promise<IChallenge[]> => {
	const challenges = await fetchSocketData('GetChallenges');
	staticData.challenges = challenges;
	return challenges;
};

const rewardCount = (c: IChallenge): number => (c.rewardItemId != null ? 1 : 0) + (c.rewardItemModId != null ? 1 : 0);

export const challengeEntity: EntityConfig<IChallenge> = {
	key: 'challenges',
	label: 'Challenges',
	singular: 'Challenge',
	glyph: 'trophy',
	blankName: 'Untitled challenge',
	retireable: true,
	newItem: (id) => {
		const challenge: IChallenge = {
			id,
			name: '',
			description: '',
			challengeTypeId: EChallengeType.EnemiesKilled,
			entityType: EEntityType.None,
			progressGoal: 10,
			designerNotes: ''
		};
		// Derive the type's statistic + entity dimension so the condition tab is consistent.
		deriveFromType(challenge, reference.challengeTypes, EChallengeType.EnemiesKilled);
		return challenge;
	},
	listBadge: (c) => reference.challengeTypeById(c.challengeTypeId)?.name ?? '—',
	badgeColor: () => 'var(--accent)',
	headline: (c) => challengeSentence(c, reference.entityName),
	meta: (c) => [
		['goal', fmtNum(c.progressGoal)],
		['', c.targetEntityId != null ? 'scoped' : 'global'],
		['reward', rewardCount(c)]
	],
	sections: [
		{
			key: 'identity',
			label: 'Identity',
			glyph: 'tag',
			desc: 'Name & player-facing description',
			kind: 'fields',
			fields: [
				{
					key: 'name',
					label: 'Challenge Name',
					type: 'text',
					placeholder: 'Name this challenge…',
					grow: true,
					required: true,
					reqMsg: 'Missing name'
				},
				{
					key: 'description',
					label: 'Description',
					type: 'textarea',
					placeholder: 'Describe the objective shown to players…',
					grow: true,
					required: true,
					reqMsg: 'No description'
				},
				{
					key: 'designerNotes',
					label: 'Designer Notes',
					type: 'textarea',
					placeholder: 'Why this challenge exists — authoring notes (never shown to players)…',
					grow: true
				}
			]
		},
		{
			key: 'condition',
			label: 'Condition',
			glyph: 'target',
			desc: 'What the player must do to complete it',
			kind: 'challenge-condition',
			// Statistic & entity are fully determined by the type, so they can't be
			// inconsistent — the remaining condition-level slips are a non-positive goal (advisory only —
			// the backend doesn't range-check it) and a DamageType-scoped statistic left Global or a
			// boss-only statistic whose target lost its boss flag (both hard-rejected on save, #2217).
			dirtyKeys: ['challengeTypeId', 'statisticType', 'entityType', 'targetEntityId', 'progressGoal'],
			warn: (c) => {
				if (!c.progressGoal || c.progressGoal <= 0) {
					return 'Goal must be greater than zero';
				}
				if (c.entityType === EEntityType.DamageType && c.targetEntityId == null) {
					return { message: 'Must target a damage type — this statistic has no global total', blocking: true };
				}
				// The target picker keeps a boss target visible even if it's since lost the
				// boss flag (reference.entityOptions' `keep` exception); flag that drift here.
				const target = c.targetEntityId != null ? staticData.enemies?.[c.targetEntityId] : undefined;
				if (target && typeBossOnly(reference.challengeTypes, c.challengeTypeId) && !target.isBoss) {
					return { message: 'Target enemy is no longer flagged as a boss', blocking: true };
				}
				return null;
			}
		},
		{
			key: 'reward',
			label: 'Reward',
			glyph: 'gift',
			desc: 'What the player unlocks on completion',
			kind: 'challenge-reward',
			count: rewardCount,
			dirtyKeys: ['rewardItemId', 'rewardItemModId'],
			warn: (c) => (c.rewardItemId == null && c.rewardItemModId == null ? 'No reward — unlocks nothing' : null)
		}
	],
	refresh,
	persist: (diff) =>
		persistEntity({
			diff,
			// The whole challenge is its primary DTO (no child collections); the server
			// re-derives statistic/entity from the type, ignoring those fields on save.
			toPrimaryDto: (c) => c,
			postPrimary: (changes) => ApiRequest.post('AdminTools/AddEditChallenges', changes),
			refresh
		})
};
