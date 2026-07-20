import { ApiRequest, fetchSocketData, type IZone, type IZoneEnemy } from '$lib/api';
import { staticData } from '$stores';
import { reference } from '../reference.svelte';
import { childChanged, guardedSave, persistEntity } from '../save-helpers';
import { firstFree } from './helpers';
import type { EntityConfig } from './types';

/** A zone plus its spawn table, derived from the enemies' embedded spawn lists. */
export interface WorkbenchZone extends IZone {
	zoneEnemies: IZoneEnemy[];
}

/**
 * Whether an enemy id resolves to a live (non-retired) enemy — the runtime spawn table
 * (`EnemiesCacheHolder.BuildZoneEnemyTables`) and the `EmptyCombatZone` lint both drop a
 * retired enemy's spawns entirely, so anything computing "will this actually spawn" must
 * agree.
 */
const isLiveEnemy = (enemyId: number): boolean => !staticData.enemies?.[enemyId]?.retiredAt;

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
		// Includes a retired enemy's spawn row: it stays visible/editable (its own enemy
		// picker shows "· retired") and, critically, must round-trip verbatim on save since
		// SetZoneEnemies fully reconciles the zone's spawn table against whatever is posted —
		// filtering it out here would delete the row from the database on the next unrelated
		// save. Consumers that need "will this actually spawn" semantics use isLiveEnemy.
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
		isHome: false,
		designerNotes: '',
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
		const boss = staticData.enemies?.[z.bossEnemyId];
		return boss ? `Boss: ${boss.name} · LV ${z.bossLevel}` : '';
	},
	sections: [
		{
			key: 'identity',
			label: 'Identity',
			glyph: 'tag',
			desc: 'Name, level range & ordering',
			kind: 'fields',
			// The dedicated-boss picker keeps an authored value visible even if it loses its
			// boss flag (reference.bossEnemyOptions' `keep` exception); flag that drift here
			// since the zone would otherwise silently point at a non-boss enemy.
			warn: (z) => {
				if (z.bossEnemyId == null || z.bossEnemyId < 0) {
					return null;
				}
				const boss = staticData.enemies?.[z.bossEnemyId];
				return boss && !boss.isBoss ? 'Dedicated boss is no longer flagged as a boss' : null;
			},
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
				},
				{
					key: 'isHome',
					label: 'Sanctuary',
					type: 'toggle',
					onLabel: 'Home (no combat)',
					offLabel: 'Combat zone'
				},
				{
					key: 'designerNotes',
					label: 'Designer Notes',
					type: 'textarea',
					placeholder: 'Why this zone exists — authoring notes (never shown to players)…',
					grow: true
				}
			]
		},
		{
			key: 'zoneEnemies',
			label: 'Enemies',
			glyph: 'skull',
			desc: 'Enemies that spawn here & their weights',
			count: (z) => z.zoneEnemies.length,
			// A retired enemy's row doesn't count toward "does something spawn here" — it never
			// rolls at runtime (mirrors the backend's EmptyCombatZone lint).
			warn: (z) => (z.isHome || z.zoneEnemies.some((ze) => isLiveEnemy(ze.enemyId)) ? null : 'No enemies spawn here'),
			kind: 'table',
			itemsKey: 'zoneEnemies',
			rowKey: 'enemyId',
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
				{
					key: '__share',
					label: 'Spawn share',
					type: 'share',
					width: 150,
					weightKey: 'weight',
					// Excludes a retired sibling's weight from the denominator — it never rolls,
					// so counting it would understate every live enemy's real share.
					shareTotal: (_row, rows) =>
						rows.reduce((sum, r) => (isLiveEnemy(r.enemyId as number) ? sum + (Number(r.weight) || 0) : sum), 0) || 1,
					// A retired enemy's own row never rolls either, so its displayed share is 0 —
					// otherwise dividing its raw weight into a denominator that already excludes it
					// could show a share over 100%.
					shareValue: (row) => (isLiveEnemy(row.enemyId as number) ? Number(row.weight) || 0 : 0)
				}
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
				isHome,
				designerNotes,
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
				isHome,
				designerNotes,
				retiredAt
			}),
			postPrimary: (changes) => ApiRequest.post('AdminTools/AddEditZones', changes),
			refresh,
			childSavers: [
				async (id, record, baseline) =>
					guardedSave(childChanged(record.zoneEnemies, baseline?.zoneEnemies), () =>
						ApiRequest.post('AdminTools/SetZoneEnemies', { zoneId: id, zoneEnemies: record.zoneEnemies })
					)
			]
		})
};
