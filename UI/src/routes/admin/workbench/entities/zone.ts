import { ApiRequest, fetchSocketData, type IZone, type IZoneEnemy } from '$lib/api';
import { staticData } from '$stores';
import { reference } from '../reference.svelte';
import { childChanged, persistEntity } from '../save-helpers';
import { firstFree } from './helpers';
import type { EntityConfig } from './types';

/** A zone plus its spawn table, derived from the enemies' embedded spawn lists. */
export interface WorkbenchZone extends IZone {
	zoneEnemies: IZoneEnemy[];
}

const refresh = async (): Promise<WorkbenchZone[]> => {
	const [zones, enemies] = await Promise.all([fetchSocketData('GetZones'), fetchSocketData('GetEnemies')]);
	staticData.zones = zones;
	staticData.enemies = enemies;
	return zones.map((zone) => ({
		...zone,
		// Normalise the optional boss FK to the select's "None" sentinel (-1) so the picker stays consistent.
		bossEnemyId: zone.bossEnemyId ?? -1,
		// Likewise normalise the optional unlock-gate FK to the "None" sentinel (-1).
		unlockChallengeId: zone.unlockChallengeId ?? -1,
		zoneEnemies: enemies.flatMap((enemy) => {
			const spawn = enemy.spawns.find((s) => s.zoneId === zone.id);
			return spawn ? [{ enemyId: enemy.id, weight: spawn.weight }] : [];
		})
	}));
};

export const zoneEntity: EntityConfig<WorkbenchZone> = {
	key: 'zones',
	label: 'Zones',
	singular: 'Zone',
	glyph: 'map',
	blankName: 'Unnamed zone',
	retireable: true,
	newItem: (id) => ({
		id,
		name: '',
		description: '',
		order: 0,
		levelMin: 1,
		levelMax: 10,
		bossEnemyId: -1,
		bossLevel: 1,
		unlockChallengeId: -1,
		zoneEnemies: []
	}),
	meta: (z) => [
		['', `L${z.levelMin}–${z.levelMax}`],
		['enemy', z.zoneEnemies.length]
	],
	headline: (z) => {
		if (z.bossEnemyId == null || z.bossEnemyId < 0) {
			return '';
		}
		const boss = (staticData.enemies ?? []).find((e) => e.id === z.bossEnemyId);
		return boss ? `Boss: ${boss.name} · LV ${z.bossLevel}` : '';
	},
	sections: [
		{
			key: 'identity',
			label: 'Identity',
			glyph: 'tag',
			desc: 'Name, level range & ordering',
			kind: 'fields',
			fields: [
				{
					key: 'name',
					label: 'Zone Name',
					type: 'text',
					placeholder: 'Name this zone…',
					grow: true,
					required: true,
					reqMsg: 'Missing name'
				},
				{ key: 'order', label: 'Order', type: 'number', width: 110 },
				{ key: 'levelMin', label: 'Level Min', type: 'number', suffix: 'lv', width: 130 },
				{ key: 'levelMax', label: 'Level Max', type: 'number', suffix: 'lv', width: 130 },
				{
					key: 'bossEnemyId',
					label: 'Dedicated Boss',
					type: 'select',
					options: reference.bossEnemyOptions,
					width: 220
				},
				{ key: 'bossLevel', label: 'Boss Level', type: 'number', suffix: 'lv', width: 130 },
				{
					key: 'unlockChallengeId',
					label: 'Unlock Challenge',
					type: 'select',
					options: reference.unlockChallengeOptions,
					width: 220
				},
				{
					key: 'description',
					label: 'Description',
					type: 'textarea',
					placeholder: 'Describe this zone…',
					grow: true,
					required: true,
					reqMsg: 'No description'
				}
			]
		},
		{
			key: 'zoneEnemies',
			label: 'Enemies',
			glyph: 'skull',
			desc: 'Enemies that spawn here & their weights',
			count: (z) => z.zoneEnemies.length,
			warn: (z) => (z.zoneEnemies.length ? null : 'No enemies spawn here'),
			kind: 'table',
			itemsKey: 'zoneEnemies',
			addLabel: 'Assign enemy',
			emptyIcon: 'skull',
			emptyTitle: 'No enemies assigned',
			emptySub: 'Nothing will spawn in this zone.',
			newRow: (z) => ({
				enemyId: firstFree(
					z.zoneEnemies.map((e) => e.enemyId),
					reference.enemyOptions()
				),
				weight: 5
			}),
			columns: [
				{ key: 'enemyId', label: 'Enemy', type: 'select', options: reference.enemyOptions, min: 220, unique: true },
				{ key: 'weight', label: 'Weight', type: 'number', align: 'r', width: 100 },
				{ key: '__share', label: 'Spawn share', type: 'share', width: 150, weightKey: 'weight' }
			]
		}
	],
	refresh,
	persist: (diff) =>
		persistEntity({
			diff,
			toPrimaryDto: ({
				id,
				name,
				description,
				order,
				levelMin,
				levelMax,
				bossEnemyId,
				bossLevel,
				unlockChallengeId,
				retiredAt
			}) => ({
				id,
				name,
				description,
				order,
				levelMin,
				levelMax,
				// Map the "None" sentinel (-1) back to an absent boss for the API.
				bossEnemyId: bossEnemyId === -1 ? undefined : bossEnemyId,
				bossLevel,
				// Likewise map the "None" sentinel (-1) back to an absent unlock gate.
				unlockChallengeId: unlockChallengeId === -1 ? undefined : unlockChallengeId,
				retiredAt
			}),
			postPrimary: (changes) => ApiRequest.post('AdminTools/AddEditZones', changes),
			refresh,
			childSavers: [
				async (id, record, baseline) => {
					if (childChanged(record.zoneEnemies, baseline?.zoneEnemies)) {
						await ApiRequest.post('AdminTools/SetZoneEnemies', { zoneId: id, zoneEnemies: record.zoneEnemies });
					}
				}
			]
		})
};
