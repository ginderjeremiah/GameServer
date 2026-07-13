/* The Zones tab's reactive layer: the progression rail + the selected zone's dossier (band, boss,
   spawn table, unlock condition, statistics). Split out of CodexView so each tab's derivations are
   independently readable/testable — see codex-view.svelte.ts for the composing orchestrator and the
   cross-tab concerns (active tab, stats fetch, cross-links). */

import type { IZone } from '$lib/api';
import { playerChallenges, staticData, statistics } from '$stores';
import {
	type EntityStatVM,
	type ZoneStatus,
	formatBand,
	liveEnemies,
	liveZones,
	resolveZoneStatus
} from './codex-display';
import { spawnShare, zoneTotalWeight } from './enemy-level';
import type { StatEntityKind } from '../stats/statistics-view.svelte';

/** The immutable per-zone fields (everything the rail row needs except the live progression status). */
export interface ZoneProjectionVM {
	id: number;
	name: string;
	band: string;
	/** Enemies (excluding the boss) that spawn in this zone. */
	spawnCount: number;
	hasBoss: boolean;
}

export interface ZoneRowVM extends ZoneProjectionVM {
	status: ZoneStatus;
}

export interface ZoneBossVM {
	enemyId: number;
	name: string;
	level: number;
}

export interface ZoneSpawnVM {
	enemyId: number;
	enemyName: string;
	share: number;
	weightLabel: string;
}

export interface ZoneUnlockVM {
	challengeId: number;
	challengeName: string;
	/** Whether the gate is still sealed (the gating challenge isn't complete). */
	sealed: boolean;
}

/** The initial-selection payload a deep-link into the Zones tab can carry. */
export interface ZonesTabPayload {
	zoneId?: number;
}

export class ZonesTabView {
	/** Inspected zone id (falls back to the head of the rail when unresolved). */
	selectedZoneId = $state<number>(-1);

	/** Projects the player's per-entity statistics (owned by the CodexView orchestrator, which fetches
	 *  them once for every tab's dossier). */
	private readonly statVMsFor: (kind: StatEntityKind, id: number) => EntityStatVM[];

	constructor(payload: ZonesTabPayload | undefined, statVMsFor: (kind: StatEntityKind, id: number) => EntityStatVM[]) {
		this.statVMsFor = statVMsFor;
		// Resolve the initial zone (the deep-link target, else the head of the rail) so the rail
		// highlights what the dossier shows. Reads stores directly to avoid touching a $derived here.
		const initialZone = this.resolveZone(payload?.zoneId ?? -1);
		if (initialZone) {
			this.selectedZoneId = initialZone.id;
		}
	}

	/* ── zones catalogue (the progression rail) ─────────────────────────────────── */

	readonly zones = $derived(liveZones(staticData.zones));

	/** Live (non-retired) enemies, memoized so the O(zones×enemies) projections below aren't rebuilt
	 *  independently of one another. */
	private readonly enemies = $derived(liveEnemies(staticData.enemies));

	/** Immutable per-zone view-models — depends only on reference data (zones + enemies). The spawn-pool
	 *  count is O(zones×enemies), so it's built once here rather than rebuilt every time a zone's
	 *  cleared/locked status changes (which happens continuously during idle play). */
	readonly zoneProjections = $derived.by<ZoneProjectionVM[]>(() => {
		const enemies = this.enemies;
		return this.zones.map((z) => ({
			id: z.id,
			name: z.name,
			band: formatBand({ min: z.levelMin, max: z.levelMax, fixed: false }),
			spawnCount: enemies.filter((e) => e.spawns.some((s) => s.zoneId === z.id)).length,
			hasBoss: z.bossEnemyId != null
		}));
	});

	/** Zone rail rows: the immutable projections overlaid with each zone's live progression status
	 *  (cleared / unlocked / locked) — the only part that reruns as zones clear or gates open. Zipped
	 *  by index since `zoneProjections` and `zones` share the same authored order and length. */
	readonly zoneRows = $derived.by<ZoneRowVM[]>(() =>
		this.zones.map((z, i) => ({
			...this.zoneProjections[i],
			status: resolveZoneStatus(statistics.isZoneCleared(z.id), this.isZoneLocked(z))
		}))
	);

	/* ── selected zone + dossier ────────────────────────────────────────────────── */

	/** The inspected zone: an explicitly-requested id resolves even when retired (see the enemy tab's
	 *  `selectedEnemy`), falling back to the head of the rail otherwise. */
	readonly selectedZone = $derived<IZone | undefined>((staticData.zones ?? [])[this.selectedZoneId] ?? this.zones[0]);

	/** The selected zone's level band (`11–22`). */
	readonly zoneBand = $derived.by<string>(() => {
		const z = this.selectedZone;
		return z ? formatBand({ min: z.levelMin, max: z.levelMax, fixed: false }) : '';
	});

	/** The selected zone's progression status (drives the dossier seal + header accent). */
	readonly selectedZoneStatus = $derived.by<ZoneStatus>(() => {
		const z = this.selectedZone;
		return z ? resolveZoneStatus(statistics.isZoneCleared(z.id), this.isZoneLocked(z)) : 'unlocked';
	});

	/** The zone's dedicated boss (its card cross-links to the enemy dossier), or null when none is authored. */
	readonly zoneBoss = $derived.by<ZoneBossVM | null>(() => {
		const z = this.selectedZone;
		if (!z || z.bossEnemyId == null) {
			return null;
		}
		const boss = (staticData.enemies ?? [])[z.bossEnemyId];
		return boss ? { enemyId: boss.id, name: boss.name, level: z.bossLevel } : null;
	});

	/** The zone's spawn table — every non-retired enemy that spawns here, with its share of the zone's
	 *  total spawn weight, ordered by share. Bosses don't populate `spawns`, so they're naturally absent. */
	readonly zoneSpawns = $derived.by<ZoneSpawnVM[]>(() => {
		const z = this.selectedZone;
		if (!z) {
			return [];
		}
		const enemies = this.enemies;
		const total = zoneTotalWeight(z.id, enemies);
		return enemies
			.flatMap((e) => {
				const spawn = e.spawns.find((s) => s.zoneId === z.id);
				return spawn ? [{ enemy: e, weight: spawn.weight }] : [];
			})
			.map(({ enemy, weight }) => ({
				enemyId: enemy.id,
				enemyName: enemy.name,
				share: spawnShare(weight, total),
				weightLabel: `weight ${weight}`
			}))
			.sort((a, b) => b.share - a.share);
	});

	/** Number of distinct enemies in the zone's spawn pool (the dossier's "N spawns" readout). */
	readonly zoneSpawnCount = $derived(this.zoneSpawns.length);

	/** The zone's unlock condition — the gating challenge's name plus whether it's still sealed — or
	 *  null for an always-open zone. */
	readonly zoneUnlock = $derived.by<ZoneUnlockVM | null>(() => {
		const z = this.selectedZone;
		if (!z || z.unlockChallengeId == null) {
			return null;
		}
		const challenge = (staticData.challenges ?? [])[z.unlockChallengeId];
		return {
			challengeId: z.unlockChallengeId,
			challengeName: challenge?.name ?? `Challenge ${z.unlockChallengeId}`,
			sealed: !playerChallenges.isChallengeCompleted(z.unlockChallengeId)
		};
	});

	/** Every statistic that references the selected zone (the zone dossier's "your record"). */
	readonly zoneStatistics = $derived.by<EntityStatVM[]>(() =>
		this.selectedZone ? this.statVMsFor('zone', this.selectedZone.id) : []
	);

	/* ── handlers ──────────────────────────────────────────────────────────────── */

	selectZone(id: number): void {
		this.selectedZoneId = id;
	}

	/* ── helpers (store reads, kept off the reactive graph for the constructor) ──── */

	/** Resolve a zone id against the full catalogue (an explicit id resolves even when retired),
	 *  falling back to the head of the rail (authored order). */
	private resolveZone(id: number): IZone | undefined {
		return (staticData.zones ?? [])[id] ?? liveZones(staticData.zones)[0];
	}

	/** A zone is locked when it carries an unlock gate the player hasn't completed yet. */
	private isZoneLocked(zone: IZone): boolean {
		return zone.unlockChallengeId != null && !playerChallenges.isChallengeCompleted(zone.unlockChallengeId);
	}
}
