import { ApiRequest, ESkillAcquisition, fetchSocketData, type IEnemy } from '$lib/api';
import { hasFlag } from '$lib/common';
import { staticData } from '$stores';
import { reference } from '../reference.svelte';
import { childChanged, guardedSave, persistEntity } from '../save-helpers';
import { firstFree } from './helpers';
import { chipsSection, type EntityConfig } from './types';

/** An enemy plus the zones it is the dedicated boss of, derived from the zones' boss FK. */
export interface WorkbenchEnemy extends IEnemy {
	/**
	 * Zone ids this enemy is the dedicated boss of (derived from each zone's `bossEnemyId`).
	 * Read-only here — the assignment is authored on the zone side; this is the inverse view.
	 */
	bossZones: number[];
}

const refresh = async (): Promise<WorkbenchEnemy[]> => {
	const [enemies, zones] = await Promise.all([fetchSocketData('GetEnemies'), fetchSocketData('GetZones')]);
	staticData.enemies = enemies;
	staticData.zones = zones;
	return enemies.map((enemy) => ({
		...enemy,
		bossZones: zones.flatMap((zone) => (zone.bossEnemyId === enemy.id ? [zone.id] : []))
	}));
};

export const enemyEntity: EntityConfig<WorkbenchEnemy> = {
	key: 'enemies',
	label: 'Enemies',
	singular: 'Enemy',
	glyph: 'skull',
	blankName: 'Unnamed enemy',
	retireable: true,
	newItem: (id) => ({
		id,
		name: '',
		isBoss: false,
		designerNotes: '',
		attributeDistribution: [],
		skillPool: [],
		spawns: [],
		bossZones: []
	}),
	listBadge: (e) => (e.isBoss ? 'Boss' : null),
	badgeColor: () => 'var(--enemy-accent)',
	meta: (e) => [
		['attr', e.attributeDistribution.length],
		['skill', e.skillPool.length],
		['zone', e.spawns.length]
	],
	// Surface the dedicated-boss assignment (the inverse of the zone's boss FK) so it's
	// visible here too; blank for an enemy that isn't any zone's dedicated boss.
	headline: (e) => {
		if (!e.bossZones.length) {
			return '';
		}
		const names = e.bossZones.map((id) => staticData.zones?.[id]?.name ?? `#${id}`).join(', ');
		return `Dedicated boss of ${names}`;
	},
	sections: [
		{
			key: 'identity',
			label: 'Identity',
			glyph: 'tag',
			desc: 'Name & classification',
			kind: 'fields',
			fields: [
				{
					key: 'name',
					label: 'Enemy Name',
					type: 'text',
					placeholder: 'Name this enemy…',
					grow: true,
					required: true,
					reqMsg: 'Missing name'
				},
				{ key: 'isBoss', label: 'Classification', type: 'toggle', onLabel: 'Boss enemy', offLabel: 'Standard enemy' },
				{
					key: 'designerNotes',
					label: 'Designer Notes',
					type: 'textarea',
					placeholder: 'Why this enemy exists — authoring notes (never shown to players)…',
					grow: true
				}
			]
		},
		{
			key: 'attrs',
			label: 'Attributes',
			glyph: 'bars',
			desc: 'Stat distribution per level',
			count: (e) => e.attributeDistribution.length,
			warn: (e) => (e.attributeDistribution.length ? null : 'No attribute distribution'),
			kind: 'table',
			itemsKey: 'attributeDistribution',
			rowKey: 'attributeId',
			addLabel: 'Add attribute',
			emptyIcon: 'bars',
			emptyTitle: 'No attributes set',
			emptySub: 'This enemy has no stat distribution yet.',
			newRow: (e) => ({
				attributeId: firstFree(
					e.attributeDistribution.map((a) => a.attributeId),
					reference.attributeOptions()
				),
				baseAmount: 0,
				amountPerLevel: 0
			}),
			columns: [
				{
					key: 'attributeId',
					label: 'Attribute',
					type: 'attribute',
					options: reference.attributeOptions,
					min: 190,
					unique: true
				},
				{ key: 'baseAmount', label: 'Base', type: 'number', align: 'r', width: 110, allowNegative: true },
				{ key: 'amountPerLevel', label: 'Per Level', type: 'number', align: 'r', width: 110, allowNegative: true }
			]
		},
		chipsSection<WorkbenchEnemy>()({
			key: 'skills',
			label: 'Skills',
			glyph: 'rune',
			desc: 'Skill pool used in battle',
			count: (e) => e.skillPool.length,
			warn: (e) => (e.skillPool.length ? null : 'No skills assigned'),
			kind: 'chips',
			itemsKey: 'skillPool',
			// Only Enemy-flagged skills can be newly assigned (the backend enforces this too); an
			// already-assigned skill that lost the flag stays visible as a removable chip.
			catalogue: () =>
				reference.skillCatalogue().map((s) => ({ ...s, addable: hasFlag(s.acquisition, ESkillAcquisition.Enemy) })),
			labelOf: (s) => s.name,
			metaOf: (s) => `${s.baseDamage} dmg`,
			emptyIcon: 'rune',
			emptyTitle: 'No skills in pool',
			emptySub: "Enemies with no skills can't act in battle.",
			addLabel: 'Add skill from pool…'
		}),
		{
			key: 'spawns',
			label: 'Spawns',
			glyph: 'pin',
			desc: 'Zones this enemy appears in',
			count: (e) => e.spawns.length,
			// A boss appears in the world via a zone's dedicated-boss FK, not its random spawn
			// table, so an assigned boss with no random spawns is valid — only warn when neither holds.
			warn: (e) => (e.spawns.length || e.bossZones.length ? null : 'Not assigned to any zone'),
			kind: 'table',
			itemsKey: 'spawns',
			rowKey: 'zoneId',
			addLabel: 'Assign zone',
			emptyIcon: 'pin',
			emptyTitle: 'Not assigned to any zone',
			emptySub: 'This enemy will never spawn in the world.',
			newRow: (e) => ({
				zoneId: firstFree(
					e.spawns.map((s) => s.zoneId),
					reference.zoneOptions()
				),
				weight: 5
			}),
			columns: [
				{ key: 'zoneId', label: 'Zone', type: 'select', options: reference.zoneOptions, min: 200, unique: true },
				{ key: 'weight', label: 'Weight', type: 'number', align: 'r', width: 100 },
				{
					key: '__share',
					label: 'Spawn share',
					type: 'share',
					width: 150,
					weightKey: 'weight',
					shareTotal: reference.enemySpawnShareTotal
				}
			]
		}
	],
	refresh,
	persist: (diff) =>
		persistEntity({
			diff,
			// Strip the child collections and the derived (read-only) bossZones off the identity DTO.
			toPrimaryDto: ({ id, name, isBoss, designerNotes, retiredAt }) => ({
				id,
				name,
				isBoss,
				designerNotes,
				attributeDistribution: [],
				skillPool: [],
				spawns: [],
				retiredAt
			}),
			postPrimary: (changes) => ApiRequest.post('AdminTools/AddEditEnemies', changes),
			refresh,
			childSavers: [
				async (id, record, baseline) =>
					guardedSave(childChanged(record.attributeDistribution, baseline?.attributeDistribution), () =>
						ApiRequest.post('AdminTools/SetEnemyAttributeDistributions', {
							enemyId: id,
							attributeDistributions: record.attributeDistribution
						})
					),
				async (id, record, baseline) =>
					guardedSave(childChanged(record.skillPool, baseline?.skillPool), () =>
						ApiRequest.post('AdminTools/SetEnemySkills', { enemyId: id, skillIds: record.skillPool })
					),
				async (id, record, baseline) =>
					guardedSave(childChanged(record.spawns, baseline?.spawns), () =>
						ApiRequest.post('AdminTools/SetEnemySpawns', { enemyId: id, spawns: record.spawns })
					)
			]
		})
};
