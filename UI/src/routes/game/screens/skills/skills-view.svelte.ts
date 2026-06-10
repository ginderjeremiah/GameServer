/* Skill loadout screen — a rail of every unlocked (and, optionally, aspirational
   locked) skill beside a large inspector, with an editable equipped band below.

   The page lets the player choose which skills (and in what order) are equipped
   for battle, capped at `playerManager.maxSelectedSkills`. Edits are atomic: each
   committed change (equip / unequip / swap / reorder) replaces the whole loadout
   through the single `SetSelectedSkills` socket command (mirroring how the backend
   models the loadout as one atomic set — see docs/spikes/179).

   IMPORTANT: the display formulas here **mirror** the battle's (`Skill.calculateDamage`,
   `Battler.takeDamage`'s defense subtraction, `Battler.cdMultiplier`) rather than calling
   them — a deliberate display-only copy, with no parity guard, tracked for consolidation
   onto one shared source in #347. They read attribute values from the same `BattleAttributes`
   the simulation uses (player allocation + equipped item/mod stats), so today the numbers
   the page shows match what the game actually fights with. */

import { EAttribute, type IChallenge, type ISkill, apiSocket } from '$lib/api';
import { BattleAttributes } from '$lib/battle';
import { playerManager, inventoryManager } from '$lib/engine';
import { staticData, toastError } from '$stores';

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

/** One attribute's contribution to a skill's raw damage. */
export interface SkillContribution {
	attributeId: EAttribute;
	multiplier: number;
	value: number;
}

/** Everything the page needs to render and rank one skill — defense-independent
 *  (effective values are derived per-render from the live Compare-vs defense). */
export interface SkillMetrics {
	skill: ISkill;
	/** True when the player owns the skill (false ⇒ locked / aspirational). */
	unlocked: boolean;
	/** Raw damage at the player's current attributes, before enemy defense. */
	rawDamage: number;
	/** Cooldown in seconds, adjusted for the player's CooldownRecovery (matches
	 *  the in-battle skill tooltip's `adjustedCd`). */
	cooldown: number;
	contributions: SkillContribution[];
	/** The challenge that rewards this skill, if any — drives the "unlocked by" line. */
	source?: IChallenge;
}

/* ── pure helpers (no reactive state — unit-tested directly) ───────────────── */

/** Effective damage after subtracting flat enemy defense (never below zero —
 *  matching the battle's `max(damage - defense, 0)`). */
export const effectiveDamage = (rawDamage: number, defense: number): number => Math.max(rawDamage - defense, 0);

/** Damage per second for a damage value over a cooldown (0 for a non-positive cooldown). */
export const damagePerSecond = (damage: number, cooldown: number): number => (cooldown > 0 ? damage / cooldown : 0);

/** Raw damage of a skill given the resolved battle attributes: base plus each
 *  multiplier applied to the attribute it scales (mirrors the battle `Skill.calculateDamage`). */
export function skillRawDamage(skill: ISkill, attributes: BattleAttributes): number {
	let damage = skill.baseDamage;
	for (const mult of skill.damageMultipliers) {
		damage += attributes.getValue(mult.attributeId) * mult.multiplier;
	}
	return damage;
}

/** Per-attribute breakdown of a skill's scaling at the given attributes. */
export function skillContributions(skill: ISkill, attributes: BattleAttributes): SkillContribution[] {
	return skill.damageMultipliers.map((mult) => ({
		attributeId: mult.attributeId,
		multiplier: mult.multiplier,
		value: attributes.getValue(mult.attributeId) * mult.multiplier
	}));
}

/** Comparator over `SkillMetrics` for the given sort + Compare-vs defense.
 *  DPS/Damage rank by *effective* value (defense-aware), descending. */
export function sortMetrics(sort: SkillSort, defense: number): (a: SkillMetrics, b: SkillMetrics) => number {
	switch (sort) {
		case 'name':
			return (a, b) => a.skill.name.localeCompare(b.skill.name);
		case 'cd':
			return (a, b) => a.cooldown - b.cooldown;
		case 'dmg':
			return (a, b) => effectiveDamage(b.rawDamage, defense) - effectiveDamage(a.rawDamage, defense);
		case 'dps':
		default:
			return (a, b) =>
				damagePerSecond(effectiveDamage(b.rawDamage, defense), b.cooldown) -
				damagePerSecond(effectiveDamage(a.rawDamage, defense), a.cooldown);
	}
}

/* ── reactive view-model ──────────────────────────────────────────────────── */

export class SkillsView {
	/** The inspected skill (rail/band selection). */
	selectedId = $state<number>(-1);
	/** Working equipped loadout, ordered by priority. Optimistically updated, then
	 *  persisted via {@link commit}; reverted if the persist fails. */
	equipped = $state<number[]>([]);
	search = $state('');
	sort = $state<SkillSort>('dps');
	filterAttributes = $state<EAttribute[]>([]);
	showLocked = $state(false);
	/** Compare-vs flat defense (0 ⇒ no defender). Drives every effective value. */
	defense = $state(0);
	modalOpen = $state(false);
	/** When set, an equip on a full loadout is awaiting the slot to replace. */
	pendingSwap = $state<number | null>(null);
	/** Index of the band card currently being dragged (reorder), if any. */
	dragIndex = $state<number | null>(null);

	constructor() {
		this.syncFromPlayer();
	}

	/** (Re)seed the working loadout + selection from the player manager. */
	syncFromPlayer(): void {
		this.equipped = [...playerManager.selectedSkills];
		if (this.selectedId < 0) {
			this.selectedId = this.equipped[0] ?? staticData.skills?.[0]?.id ?? -1;
		}
	}

	/** The loadout cap (number of equip slots). */
	readonly cap = $derived(playerManager.maxSelectedSkills);

	/** Ids the player has unlocked. */
	readonly unlockedIds = $derived(playerManager.unlockedSkills.map((s) => s.skillId));

	/** Skills shown on the page: every non-retired skill, plus any retired skill the
	 *  player still owns (retirement keeps owned records resolvable). */
	readonly catalogue = $derived(
		(staticData.skills ?? []).filter((s) => this.unlockedIds.includes(s.id) || !s.retiredAt)
	);

	/** The player's resolved battle attributes (allocation + equipped item/mod stats),
	 *  the same composition the battle uses. */
	readonly battleAttributes = $derived(
		new BattleAttributes([...playerManager.attributes, ...inventoryManager.equipmentStats], true)
	);

	/** Defense-independent metrics per catalogue skill, keyed by id. Recomputed
	 *  wholesale when attributes/equipment change — read via {@link metric}. */
	readonly metricsById = $derived.by(() => {
		const attrs = this.battleAttributes;
		const cdMultiplier = 1 + attrs.getValue(EAttribute.CooldownRecovery) / 100;
		const challenges = staticData.challenges ?? [];
		const byId: Record<number, SkillMetrics> = {};
		for (const skill of this.catalogue) {
			byId[skill.id] = {
				skill,
				unlocked: this.unlockedIds.includes(skill.id),
				rawDamage: skillRawDamage(skill, attrs),
				cooldown: cdMultiplier > 0 ? skill.cooldownMs / 1000 / cdMultiplier : skill.cooldownMs / 1000,
				contributions: skillContributions(skill, attrs),
				source: challenges.find((c) => c.rewardSkillId === skill.id)
			};
		}
		return byId;
	});

	/** Metrics for a single skill id (undefined when not in the catalogue). */
	metric(id: number): SkillMetrics | undefined {
		return this.metricsById[id];
	}

	/** Slider ceiling: enough defense to fully block the biggest hit. */
	readonly maxDamage = $derived(Math.max(1, ...this.catalogue.map((s) => this.metricsById[s.id]?.rawDamage ?? 0)));

	/** Core attributes any catalogue skill scales on (the offered filter chips). */
	readonly usedAttributes = $derived(
		FILTERABLE_ATTRIBUTES.filter((attr) =>
			this.catalogue.some((s) => s.damageMultipliers.some((m) => m.attributeId === attr))
		)
	);

	/** Number of active library filters (badge on the Sort·Filter button). */
	readonly activeFilterCount = $derived(this.filterAttributes.length + (this.showLocked ? 1 : 0));

	/** Filtered + sorted metrics (the rail's full list before equipped/available split). */
	readonly railList = $derived.by(() => {
		const term = this.search.trim().toLowerCase();
		const list = this.catalogue
			.map((s) => this.metricsById[s.id])
			.filter((m): m is SkillMetrics => m != null)
			.filter((m) => {
				if (!this.showLocked && !m.unlocked) {
					return false;
				}
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
		return list.sort(sortMetrics(this.sort, this.defense));
	});

	/** Equipped rail rows, ordered by loadout slot (not by the active sort). */
	readonly equippedRail = $derived(
		this.railList
			.filter((m) => this.isEquipped(m.skill.id))
			.sort((a, b) => this.slotOf(a.skill.id) - this.slotOf(b.skill.id))
	);

	/** Available (unequipped) rail rows, in the active sort order. */
	readonly availableRail = $derived(this.railList.filter((m) => !this.isEquipped(m.skill.id)));

	/** Metrics for the inspected skill, falling back to the first listed skill. */
	readonly selected = $derived(this.metricsById[this.selectedId] ?? this.railList[0]);

	/** Combined effective DPS of the equipped loadout vs the current defense. */
	readonly combinedEffectiveDps = $derived(this.equipped.reduce((sum, id) => sum + this.effectiveDps(id), 0));

	/** Combined effective single-hit burst of the equipped loadout vs the current defense. */
	readonly combinedEffectiveBurst = $derived(this.equipped.reduce((sum, id) => sum + this.effective(id), 0));

	/* ── per-skill effective reads (defense-aware) ───────────────────────────── */

	isEquipped(id: number): boolean {
		return this.equipped.includes(id);
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

	effective(id: number): number {
		return effectiveDamage(this.rawDamage(id), this.defense);
	}

	effectiveDps(id: number): number {
		return damagePerSecond(this.effective(id), this.cooldown(id));
	}

	/** Defense actually applied to a skill (clamped to its raw damage, for display). */
	appliedDefense(id: number): number {
		return Math.min(this.defense, this.rawDamage(id));
	}

	/* ── selection + loadout edits ───────────────────────────────────────────── */

	select(id: number): void {
		this.selectedId = id;
		this.pendingSwap = null;
	}

	/** Equip / unequip a skill. Equipping into a full loadout starts a swap instead. */
	toggle(id: number): void {
		if (!this.unlockedIds.includes(id)) {
			return;
		}
		if (this.isEquipped(id)) {
			void this.commit(this.equipped.filter((x) => x !== id));
		} else if (this.equipped.length < this.cap) {
			void this.commit([...this.equipped, id]);
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
		void this.commit(next);
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
		void this.commit(next);
	}

	setDefense(defense: number): void {
		this.defense = Math.max(0, Math.min(defense, this.maxDamage));
	}

	setSort(sort: SkillSort): void {
		this.sort = sort;
	}

	toggleAttributeFilter(attr: EAttribute): void {
		this.filterAttributes = this.filterAttributes.includes(attr)
			? this.filterAttributes.filter((a) => a !== attr)
			: [...this.filterAttributes, attr];
	}

	toggleShowLocked(): void {
		this.showLocked = !this.showLocked;
	}

	resetFilters(): void {
		this.sort = 'dps';
		this.filterAttributes = [];
		this.showLocked = false;
	}

	/* ── persistence ─────────────────────────────────────────────────────────── */

	/** Optimistically apply a new loadout, persist it atomically, and revert on failure. */
	private async commit(next: number[]): Promise<void> {
		const previous = this.equipped;
		this.equipped = next;
		this.applyToPlayer(next);
		try {
			const response = await apiSocket.sendSocketCommand('SetSelectedSkills', next);
			if (response.error) {
				throw new Error(response.error);
			}
		} catch {
			this.equipped = previous;
			this.applyToPlayer(previous);
			toastError('Your loadout could not be saved. Please try again.');
		}
	}

	/** Mirror the loadout onto the player manager so battles and other screens read
	 *  the new equipped set/order without a reload (reassigned so the reactive proxy
	 *  observes the change). */
	private applyToPlayer(orderedIds: number[]): void {
		playerManager.unlockedSkills = playerManager.unlockedSkills.map((s) => ({
			...s,
			selected: orderedIds.includes(s.skillId),
			order: orderedIds.includes(s.skillId) ? orderedIds.indexOf(s.skillId) : 0
		}));
	}
}
