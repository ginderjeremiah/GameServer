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
		zoneEnemies: enemies
			.filter((enemy) => enemy.spawns.some((spawn) => spawn.zoneId === zone.id))
			.map((enemy) => ({
				enemyId: enemy.id,
				weight: enemy.spawns.find((spawn) => spawn.zoneId === zone.id)!.weight
			}))
	}));
};

export const zoneEntity: EntityConfig<WorkbenchZone> = {
	key: 'zones',
	label: 'Zones',
	singular: 'Zone',
	glyph: 'map',
	blankName: 'Unnamed zone',
	newItem: (id) => ({ id, name: '', description: '', order: 0, levelMin: 1, levelMax: 10, zoneEnemies: [] }),
	meta: (z) => [
		['', `L${z.levelMin}–${z.levelMax}`],
		['enemy', z.zoneEnemies.length]
	],
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
			toPrimaryDto: ({ id, name, description, order, levelMin, levelMax }) => ({
				id,
				name,
				description,
				order,
				levelMin,
				levelMax
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
