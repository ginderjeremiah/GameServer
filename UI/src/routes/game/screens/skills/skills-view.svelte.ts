/* Skill loadout screen — a rail of the player's unlocked skills beside a large
   inspector, with an editable equipped band below.

   The page lets the player choose which skills (and in what order) are equipped
   for battle, capped at the generated `MAX_SELECTED_SKILLS`. Edits are atomic: each
   committed change (equip / unequip / swap / reorder) replaces the whole loadout
   through the single `SetSelectedSkills` socket command (mirroring how the backend
   models the loadout as one atomic set — see docs/spikes/179).

   The display numbers come straight from the shared pure battle formulas
   (`$lib/battle/battle-formulas` — the same functions `Skill.calculateDamage`,
   `Battler.takeDamage` and `Battler.cdMultiplier` delegate to), computed against the player's live
   `BattleAttributes` from the single shared `composePlayerBattleAttributes` builder the battle engine
   also consumes (allocation + equipped gear, class locked base, proficiency bonuses, and the class
   signature passive), so the page shows what the game actually fights with by construction (#347, #1500). */

import { EAttribute, type ISkill, type IZone, apiSocket } from '$lib/api';
import { MAX_SELECTED_SKILLS } from '$lib/api/types/game-constants';
import {
	toughnessMitigatedDamage,
	calculateSkillDamage,
	composePlayerBattleAttributes,
	cooldownMultiplier,
	expectedCritMultiplier,
	isSkillDormant,
	skillContributions,
	type SkillContribution
} from '$lib/battle';
import { damagePerSecond, enemyToughness, SerializedQueue } from '$lib/common';
import { playerManager, inventoryManager } from '$lib/engine';
import { staticData, toastError, playerProficiencies } from '$stores';

export type SkillSort = 'dps' | 'dmg' | 'cd' | 'name';

export const SKILL_SORTS: { key: SkillSort; label: string }[] = [
	{ key: 'dps', label: 'DPS' },
	{ key: 'dmg', label: 'Damage' },
	{ key: 'cd', label: 'Cooldown' },
	{ key: 'name', label: 'Name' }
];

/** The six core attributes a skill can scale on, in display order — the only
 *  attributes offered in the attribute filter. */
export const FILTERABLE_ATTRIBUTES: EAttribute[] = [
	EAttribute.Strength,
	EAttribute.Endurance,
	EAttribute.Intellect,
	EAttribute.Agility,
	EAttribute.Dexterity,
	EAttribute.Luck
];

/** Everything the page needs to render and rank one skill — Toughness-independent
 *  (effective values are derived per-render from the live Compare-vs Toughness). */
export interface SkillMetrics {
	skill: ISkill;
	/** Raw damage at the player's current attributes, before enemy mitigation. */
	rawDamage: number;
	/** Cooldown in seconds, adjusted for the player's CooldownRecovery (matches
	 *  the in-battle skill tooltip's `adjustedCd`). */
	cooldown: number;
	contributions: SkillContribution[];
	/** This skill's own effective critical-hit chance — its authored base (#1453) scaled by the
	 *  player's CriticalChanceMultiplier — so an unauthored skill (0) never shows a crit row. */
	critChance: number;
	/** The long-run average damage multiplier from this skill's crits (≥1), folded into every
	 *  effective-damage read below so the page mirrors the in-fight skill tooltip and the battle.
	 *  Per-skill because the enabler is now the skill's own base chance, not a build-wide stat; the
	 *  live battle still rolls each crit individually. */
	critMultiplier: number;
}

/** A read-only innate skill granted by an equipped item — shown in the loadout band but not selectable,
 *  removable, or reorderable. Its presence and order follow the equipped gear, not the player's loadout. */
export interface InnateSkill {
	skill: ISkill;
	/** The equipped item granting it, labelling the read-only card with its source. */
	sourceItemName: string;
	/** True when this grant duplicates a selected skill (or an earlier grant): it is already in the loadout
	 *  and fielded once, surfaced so the dropped duplicate isn't silently invisible. */
	duplicate: boolean;
}

/** A Compare-vs quick-pick: an enemy from the current zone whose Toughness snaps the slider. */
export interface ComparePreset {
	/** Stable identity — distinguishes the boss pill from a same-id idle-spawn pill. */
	key: string;
	name: string;
	/** True for the zone's dedicated boss (evaluated at its fixed level), false for an idle spawn. */
	isBoss: boolean;
	/** The enemy's Toughness at its evaluated level. */
	toughness: number;
}

/* ── pure helpers (no reactive state — unit-tested directly) ───────────────── */

/** Comparator over `SkillMetrics` for the given sort + Compare-vs Toughness.
 *  DPS/Damage rank by *effective* value (mitigation-aware), descending. Each skill's own
 *  `critMultiplier` scales its raw damage before mitigation (mirroring {@link SkillsView.effective}) —
 *  per-skill because the crit enabler is now the skill's own base chance (#1453), not a build-wide
 *  stat — so the ranking matches the crit-inclusive numbers the rows display. */
export function sortMetrics(sort: SkillSort, toughness: number): (a: SkillMetrics, b: SkillMetrics) => number {
	const mitigated = (m: SkillMetrics) => toughnessMitigatedDamage(m.rawDamage * m.critMultiplier, toughness);
	switch (sort) {
		case 'name':
			return (a, b) => a.skill.name.localeCompare(b.skill.name);
		case 'cd':
			return (a, b) => a.cooldown - b.cooldown;
		case 'dmg':
			return (a, b) => mitigated(b) - mitigated(a);
		case 'dps':
		default:
			return (a, b) => damagePerSecond(mitigated(b), b.cooldown) - damagePerSecond(mitigated(a), a.cooldown);
	}
}

/** The representative level for a zone's idle spawns: the midpoint of its encounter range.
 *  Spawns roll uniformly in [levelMin, levelMax] and Toughness scales linearly with level, so the
 *  midpoint is the expected encounter level — and thus the expected Toughness — without a server roll. */
export function zoneSpawnLevel(zone: IZone): number {
	return (zone.levelMin + zone.levelMax) / 2;
}

/* ── reactive view-model ──────────────────────────────────────────────────── */

export class SkillsView {
	/** The inspected skill (rail/band selection). */
	selectedId = $state<number>(-1);
	/** Pending optimistic loadout while a commit is in flight (or after the latest commit failed without
	 *  a newer edit); null when the screen mirrors the player manager. {@link equipped} reads through it. */
	private pendingLoadout = $state<number[] | null>(null);
	/** Last loadout the server confirmed — the rollback target when the latest commit fails. */
	private committed: number[] = [];
	/** Serializes the optimistic commits so persists apply in edit order and rollback baselines never
	 *  interleave (mirrors the inventory manager's serialized mutations). */
	private queue = new SerializedQueue();
	/** Monotonic edit counter; a failed persist rolls back only when it was the latest edit issued. */
	private commitSeq = 0;
	search = $state('');
	sort = $state<SkillSort>('dps');
	filterAttributes = $state<EAttribute[]>([]);
	/** Compare-vs Toughness (0 ⇒ no defender). Drives every effective value through the mitigation curve. */
	toughness = $state(0);
	/** The selected Compare-vs preset pill, or null when Toughness was set manually (slider drag). */
	selectedPresetKey = $state<string | null>(null);
	modalOpen = $state(false);
	/** When set, an equip on a full loadout is awaiting the slot to replace. */
	pendingSwap = $state<number | null>(null);
	/** Index of the band card currently being dragged (reorder), if any. */
	dragIndex = $state<number | null>(null);

	constructor() {
		this.syncFromPlayer();
	}

	/** (Re)seed the committed baseline + inspector selection from the player manager. */
	syncFromPlayer(): void {
		this.committed = [...playerManager.selectedSkills];
		this.pendingLoadout = null;
		if (this.selectedId < 0) {
			this.selectedId = this.equipped[0] ?? staticData.skills?.[0]?.id ?? -1;
		}
	}

	/** Working equipped loadout, ordered by priority: the pending optimistic edit while a commit is in
	 *  flight, otherwise the player manager's committed loadout — so an external loadout change (e.g. a
	 *  challenge skill unlock or server reconciliation) is reflected once no edit is pending. */
	readonly equipped = $derived(this.pendingLoadout ?? [...playerManager.selectedSkills]);

	/** The loadout cap (number of equip slots) — the single generated game constant. */
	readonly cap = MAX_SELECTED_SKILLS;

	/** Ids the player has unlocked, as a set for O(1) membership (the catalogue filter and the
	 *  equip-ownership guard). Rebuilt from reactive deps on each change (a lookup, not held state). */
	// eslint-disable-next-line svelte/prefer-svelte-reactivity
	readonly unlockedIds = $derived(new Set(playerManager.unlockedSkills.map((s) => s.skillId)));

	/** Skills shown on the page: only the skills the player has unlocked (including any retired
	 *  skill they still own — retirement keeps owned records resolvable). "How do I get more
	 *  skills?" is answered by the Codex, not here. */
	readonly catalogue = $derived((staticData.skills ?? []).filter((s) => this.unlockedIds.has(s.id)));

	/** The equipped weapon's type, driving the weapon-match grey-out (#1342). An empty weapon slot resolves to
	 *  the virtual-fists `Unarmed`, exactly as the battle assembly keys the gate. */
	readonly equippedWeaponType = $derived(inventoryManager.equippedWeaponType);

	/** The player's resolved live battle attributes — allocation + equipped gear, the class locked base
	 *  and proficiency bonuses, and the class signature passive, assembled by the single shared builder
	 *  the live battler also consumes ({@link composePlayerBattleAttributes}), so this screen can't drift
	 *  from what the player actually fights with (#1500). */
	readonly battleAttributes = $derived(
		composePlayerBattleAttributes(
			playerManager.attributes,
			inventoryManager.equipmentStats,
			playerManager.battleLockedBaseModifiers,
			playerProficiencies.battleModifiers,
			(resolve) => playerManager.battleSignaturePassiveModifier(resolve)
		)
	);

	/** The player's CriticalDamage multiplier (≥1) — still build-wide, unlike the chance itself (#1453),
	 *  since only the ENABLER (a skill's own base chance) moved onto the skill. Folded into each skill's
	 *  {@link SkillMetrics.critMultiplier} below. */
	readonly critDamage = $derived(this.battleAttributes.getValue(EAttribute.CriticalDamage));

	/** Ids to compute battle metrics for: every unlocked (catalogue) skill plus every skill the equipped
	 *  gear grants. Innate grants are deliberately never unlocked, so without this the innate band's
	 *  read-only cards would silently lose their dmg/cd/dps block (#1500). */
	readonly metricsSkillIds = $derived.by(() => {
		// eslint-disable-next-line svelte/prefer-svelte-reactivity
		const ids = new Set(this.catalogue.map((s) => s.id));
		for (const id of inventoryManager.grantedSkillIds) {
			ids.add(id);
		}
		return ids;
	});

	/** Toughness-independent metrics per {@link metricsSkillIds} entry, keyed by id. Recomputed
	 *  wholesale when attributes/equipment change — read via {@link metric}. */
	readonly metricsById = $derived.by(() => {
		const attrs = this.battleAttributes;
		const cdMultiplier = cooldownMultiplier(attrs);
		const critChanceMultiplier = attrs.getValue(EAttribute.CriticalChanceMultiplier);
		const skills = staticData.skills ?? [];
		const byId: Record<number, SkillMetrics> = {};
		for (const id of this.metricsSkillIds) {
			const skill = skills[id];
			if (!skill) {
				continue;
			}
			// Each skill's own base CriticalChance (#1453) is the opt-in enabler; the player's
			// CriticalChanceMultiplier only scales it, so an unauthored skill (0) stays inert.
			const critChance = skill.criticalChance * critChanceMultiplier;
			byId[skill.id] = {
				skill,
				rawDamage: calculateSkillDamage(skill, attrs),
				cooldown: cdMultiplier > 0 ? skill.cooldownMs / 1000 / cdMultiplier : skill.cooldownMs / 1000,
				contributions: skillContributions(skill, attrs),
				critChance,
				critMultiplier: expectedCritMultiplier(critChance, this.critDamage)
			};
		}
		return byId;
	});

	/** Metrics for a single skill id (undefined when neither unlocked nor granted by equipped gear). */
	metric(id: number): SkillMetrics | undefined {
		return this.metricsById[id];
	}

	/** Enemy quick-picks for the Compare-vs bar, sourced from the player's current zone: every
	 *  non-retired enemy that spawns there (Toughness at the range midpoint) plus the zone's dedicated
	 *  boss (Toughness at its fixed level). Empty until zone/enemy reference data is loaded. */
	readonly comparePresets = $derived.by<ComparePreset[]>(() => {
		const zone = staticData.zones?.[playerManager.currentZone];
		if (!zone) {
			return [];
		}
		const enemies = staticData.enemies ?? [];
		const spawnLevel = zoneSpawnLevel(zone);
		const presets: ComparePreset[] = enemies
			.filter((enemy) => !enemy.retiredAt && enemy.spawns.some((spawn) => spawn.zoneId === zone.id))
			.map((enemy) => ({
				key: `spawn-${enemy.id}`,
				name: enemy.name,
				isBoss: false,
				toughness: enemyToughness(enemy, spawnLevel)
			}));
		const boss = zone.bossEnemyId == null ? undefined : enemies[zone.bossEnemyId];
		if (boss) {
			presets.push({
				key: `boss-${boss.id}`,
				name: boss.name,
				isBoss: true,
				toughness: enemyToughness(boss, zone.bossLevel)
			});
		}
		return presets;
	});

	/** Slider ceiling: the toughest Toughness among the Compare-vs presets, so every enemy you currently face
	 *  is reachable on the slider. The mitigation curve asymptotes (no value fully blocks a hit), so there is no
	 *  natural "block everything" ceiling — this caps exploration at your hardest current target. Falls back to
	 *  1 until zone/enemy reference data loads. */
	readonly maxToughness = $derived(Math.max(1, ...this.comparePresets.map((p) => p.toughness)));

	/** Core attributes any catalogue skill scales on (the offered filter chips). */
	readonly usedAttributes = $derived(
		FILTERABLE_ATTRIBUTES.filter((attr) =>
			this.catalogue.some((s) => s.damageMultipliers.some((m) => m.attributeId === attr))
		)
	);

	/** Number of active library filters (badge on the Sort·Filter button). */
	readonly activeFilterCount = $derived(this.filterAttributes.length);

	/** Filtered + sorted metrics (the rail's full list before equipped/available split). */
	readonly railList = $derived.by(() => {
		const term = this.search.trim().toLowerCase();
		const list = this.catalogue
			.map((s) => this.metricsById[s.id])
			.filter((m): m is SkillMetrics => m != null)
			.filter((m) => {
				if (term && !m.skill.name.toLowerCase().includes(term)) {
					return false;
				}
				if (
					this.filterAttributes.length &&
					!m.skill.damageMultipliers.some((mm) => this.filterAttributes.includes(mm.attributeId))
				) {
					return false;
				}
				return true;
			});
		return list.sort(sortMetrics(this.sort, this.toughness));
	});

	/** Equipped rail rows, ordered by loadout slot (not by the active sort). Built from
	 *  the unfiltered loadout so a search/attribute filter can't drop an equipped row while
	 *  the header still counts it — filtering is reserved for {@link availableRail}. */
	readonly equippedRail = $derived(
		this.equipped.map((id) => this.metricsById[id]).filter((m): m is SkillMetrics => m != null)
	);

	/** Available (unequipped) rail rows, in the active sort order. */
	readonly availableRail = $derived(this.railList.filter((m) => !this.isEquipped(m.skill.id)));

	/** Skills granted by the equipped items (innate, always-active while equipped), in EEquipmentSlot order
	 *  — read-only loadout entries additive to and exempt from the selectable cap. De-duplicated against the
	 *  selected loadout and each other (first wins), mirroring the battle assembly: a duplicate is flagged so
	 *  the band can surface "already in your loadout" rather than dropping it invisibly. */
	readonly innateSkills = $derived.by<InnateSkill[]>(() => {
		const skills = staticData.skills ?? [];
		// A transient local set (selected ids field these grants), not reactive state.
		// eslint-disable-next-line svelte/prefer-svelte-reactivity
		const seen = new Set(this.equipped);
		const result: InnateSkill[] = [];
		for (const item of inventoryManager.equippedSlots) {
			if (!item || item.grantedSkillId == null) {
				continue;
			}
			const skill = skills[item.grantedSkillId];
			if (!skill) {
				continue;
			}
			const duplicate = seen.has(item.grantedSkillId);
			seen.add(item.grantedSkillId);
			result.push({ skill, sourceItemName: item.name, duplicate });
		}
		return result;
	});

	/** Metrics for the inspected skill, falling back to the first listed skill. */
	readonly selected = $derived(this.metricsById[this.selectedId] ?? this.railList[0]);

	/** Combined effective DPS of the equipped loadout vs the current Toughness. */
	readonly combinedEffectiveDps = $derived(this.equipped.reduce((sum, id) => sum + this.effectiveDps(id), 0));

	/** Combined effective single-hit burst of the equipped loadout vs the current Toughness. */
	readonly combinedEffectiveBurst = $derived(this.equipped.reduce((sum, id) => sum + this.effective(id), 0));

	/* ── per-skill effective reads (Toughness-aware) ─────────────────────────── */

	isEquipped(id: number): boolean {
		return this.equipped.includes(id);
	}

	/** Whether a skill is dormant under the equipped weapon — a weapon-leaf-typed skill whose type doesn't
	 *  match the held weapon (weapon-agnostic skills never dim). Derived from the same `isFielded` rule the
	 *  battle assembly applies, so the greyed cards can't diverge from the skills actually fielded (#1342). */
	dormant(skill: ISkill): boolean {
		return isSkillDormant(skill, this.equippedWeaponType);
	}

	/** 1-based loadout slot of an equipped skill (0 when not equipped). */
	slotOf(id: number): number {
		return this.equipped.indexOf(id) + 1;
	}

	rawDamage(id: number): number {
		return this.metricsById[id]?.rawDamage ?? 0;
	}

	cooldown(id: number): number {
		return this.metricsById[id]?.cooldown ?? 0;
	}

	/** Effective single-hit damage: raw damage scaled by the skill's own expected crit multiplier (a crit
	 *  applies before mitigation, so it scales first), then run through the Compare-vs Toughness curve. */
	effective(id: number): number {
		const critMultiplier = this.metricsById[id]?.critMultiplier ?? 1;
		return toughnessMitigatedDamage(this.rawDamage(id) * critMultiplier, this.toughness);
	}

	effectiveDps(id: number): number {
		return damagePerSecond(this.effective(id), this.cooldown(id));
	}

	/** Expected crit damage folded into a skill's hit before mitigation (`raw × (critMultiplier − 1)`),
	 *  0 when no crit can occur — drives the breakdown's Critical row and the raw-note's crit clause. */
	critBonus(id: number): number {
		const critMultiplier = this.metricsById[id]?.critMultiplier ?? 1;
		return this.rawDamage(id) * (critMultiplier - 1);
	}

	/** Damage the Compare-vs Toughness curve removes from a skill's crit-scaled hit (`raw×crit − effective`),
	 *  so the breakdown's raw + crit − mitigation reconciles with {@link effective}. */
	mitigatedAmount(id: number): number {
		const critMultiplier = this.metricsById[id]?.critMultiplier ?? 1;
		return this.rawDamage(id) * critMultiplier - this.effective(id);
	}

	/* ── selection + loadout edits ───────────────────────────────────────────── */

	select(id: number): void {
		this.selectedId = id;
		this.pendingSwap = null;
	}

	/** Equip / unequip a skill. Equipping into a full loadout starts a swap instead. */
	toggle(id: number): void {
		if (!this.unlockedIds.has(id)) {
			return;
		}
		if (this.isEquipped(id)) {
			this.commit(this.equipped.filter((x) => x !== id));
		} else if (this.equipped.length < this.cap) {
			this.commit([...this.equipped, id]);
		} else {
			this.pendingSwap = id;
		}
	}

	/** Resolve a pending swap by replacing the chosen slot with the incoming skill. */
	swapInto(slotIndex: number): void {
		const incoming = this.pendingSwap;
		if (incoming == null || slotIndex < 0 || slotIndex >= this.equipped.length) {
			return;
		}
		const next = this.equipped.map((x, i) => (i === slotIndex ? incoming : x));
		this.selectedId = incoming;
		this.pendingSwap = null;
		this.commit(next);
	}

	cancelSwap(): void {
		this.pendingSwap = null;
	}

	/** Move an equipped skill from one slot to another (drag-to-reorder). */
	reorder(from: number, to: number): void {
		if (from === to || from < 0 || to < 0 || from >= this.equipped.length || to >= this.equipped.length) {
			return;
		}
		const next = [...this.equipped];
		const [moved] = next.splice(from, 1);
		next.splice(to, 0, moved);
		this.commit(next);
	}

	setToughness(toughness: number): void {
		this.toughness = Math.max(0, Math.min(toughness, this.maxToughness));
		// A manual slider drag drops the active preset selection (mirrors the mock).
		this.selectedPresetKey = null;
	}

	/** Snap the Compare-vs Toughness to an enemy preset and mark its pill selected. */
	selectPreset(preset: ComparePreset): void {
		this.toughness = Math.max(0, Math.min(preset.toughness, this.maxToughness));
		this.selectedPresetKey = preset.key;
	}

	setSort(sort: SkillSort): void {
		this.sort = sort;
	}

	toggleAttributeFilter(attr: EAttribute): void {
		this.filterAttributes = this.filterAttributes.includes(attr)
			? this.filterAttributes.filter((a) => a !== attr)
			: [...this.filterAttributes, attr];
	}

	resetFilters(): void {
		this.sort = 'dps';
		this.filterAttributes = [];
	}

	/* ── persistence ─────────────────────────────────────────────────────────── */

	/** Optimistically apply a new loadout, persist it atomically, and revert on failure. The player
	 *  manager owns the loadout mutation (so battles/other screens see it without a reload).
	 *
	 *  Commits are serialized on {@link queue} so their persists apply in edit order and never interleave
	 *  rollback baselines: only the *latest* edit's failure rolls back — to the last server-confirmed
	 *  loadout ({@link committed}), not a per-call snapshot — so an earlier failure can't silently discard
	 *  a later successful edit. The optimistic write itself is synchronous, so rapid edits read fresh state
	 *  and queue rather than racing. */
	private commit(next: number[]): void {
		this.pendingLoadout = next;
		playerManager.setSelectedSkills(next);
		const seq = ++this.commitSeq;
		this.queue.run(async () => {
			const response = await apiSocket.sendSocketCommand('SetSelectedSkills', next);
			const isLatest = seq === this.commitSeq;
			if (response.error) {
				if (isLatest) {
					playerManager.setSelectedSkills(this.committed);
					this.pendingLoadout = null;
					toastError('Your loadout could not be saved. Please try again.');
				}
			} else {
				// Advance the baseline on *every* success, not just the latest edit's: persists are
				// serialized, so when an earlier edit succeeds and a later one then fails, the server
				// holds the earlier edit's loadout — which is exactly what the latest failure must roll
				// back to. Advancing only for the latest would roll back past that confirmed state.
				this.committed = next;
				if (isLatest) {
					this.pendingLoadout = null;
				}
			}
		});
	}
}
