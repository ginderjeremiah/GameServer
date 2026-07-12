/* The Skills tab's reactive layer: the reference skill table + the selected skill's dossier (scaling,
   effects, used-by, provenance, statistics). Split out of CodexView so each tab's derivations are
   independently readable/testable — see codex-view.svelte.ts for the composing orchestrator and the
   cross-tab concerns (active tab, stats fetch, cross-links). */

import { ERarity, type ISkill } from '$lib/api';
import {
	attributeCode,
	attributeColor,
	attributeIsHarmful,
	attributeName,
	describeEffect,
	effectDirectionColor,
	formatNum,
	rarityColor,
	rarityLabel
} from '$lib/common';
import { staticData } from '$stores';
import {
	SKILL_ACQUISITION_EMPTY,
	SKILL_SOURCE_LABEL,
	enemyAccent,
	formatBaseDamage,
	formatCooldown,
	liveEnemies,
	liveSkills
} from './codex-display';
import type { EntityStatVM } from './codex-view.svelte';
import { type SkillAcquisitionStatus, resolveSkillProvenance } from './skill-provenance';
import type { StatEntityKind } from '../stats/statistics-view.svelte';

export interface SkillRowVM {
	id: number;
	name: string;
	/** Base damage, or `—` for a utility skill. */
	baseDamageLabel: string;
	/** Cooldown, or `—` for an instant/utility skill. */
	cooldownLabel: string;
	/** How many (non-retired) enemies have this skill in their pool. */
	usedByCount: number;
	/** Themeable rarity hue for the row's tier mark. */
	rarityColor: string;
}

/** The selected skill's rarity tier (hue + label) for the dossier header. */
export interface SkillRarityVM {
	color: string;
	label: string;
}

export interface SkillScalingVM {
	attributeId: number;
	name: string;
	code: string;
	/** Magnitude badge, e.g. `×1.5`. */
	multiplierLabel: string;
	color: string;
}

export interface SkillEffectVM {
	id: number;
	/** Signed/`×` magnitude badge, e.g. `+15` or `×0.5`. */
	magnitude: string;
	attributeName: string;
	/** `self` / `enemy`, the side the effect lands on. */
	targetLabel: string;
	/** Duration in seconds, e.g. `5s`. */
	duration: string;
	/** Themed buff/debuff accent for the magnitude. */
	color: string;
}

export interface SkillUserVM {
	enemyId: number;
	name: string;
	isBoss: boolean;
	accent: string;
}

/** A "how to obtain" source row — an item that grants the skill. */
export interface SkillSourceVM {
	kind: 'item';
	id: number;
	/** "Granted by" lead-in. */
	label: string;
	name: string;
	/** Themed accent: the granting item's rarity hue. */
	accent: string;
}

/** The skill dossier's acquisition section: concrete sources, plus the wording for the no-source case. */
export interface SkillProvenanceVM {
	status: SkillAcquisitionStatus;
	sources: SkillSourceVM[];
	/** Wording for the no-source case (empty when sources exist). */
	emptyLabel: string;
}

/** The initial-selection payload a deep-link into the Skills tab can carry. */
export interface SkillsTabPayload {
	skillId?: number;
}

export class SkillsTabView {
	/** Inspected skill id (falls back to the head of the catalogue when unresolved). */
	selectedSkillId = $state<number>(-1);

	/** Projects the player's per-entity statistics (owned by the CodexView orchestrator, which fetches
	 *  them once for every tab's dossier). */
	private readonly statVMsFor: (kind: StatEntityKind, id: number) => EntityStatVM[];

	constructor(payload: SkillsTabPayload | undefined, statVMsFor: (kind: StatEntityKind, id: number) => EntityStatVM[]) {
		this.statVMsFor = statVMsFor;
		// …resolve the skill table (the deep-link target, else the head of the catalogue).
		const initialSkill = this.resolveSkill(payload?.skillId ?? -1);
		if (initialSkill) {
			this.selectedSkillId = initialSkill.id;
		}
	}

	/* ── skills catalogue (the reference skill table) ────────────────────────────── */

	readonly skillsCatalogue = $derived(liveSkills(staticData.skills));

	/** Live (non-retired) enemies — needed for the used-by projections below. */
	private readonly enemies = $derived(liveEnemies(staticData.enemies));

	/** Skill table rows: name, base damage, cooldown and how many enemies use the skill. */
	readonly skillRows = $derived.by<SkillRowVM[]>(() => {
		const enemies = this.enemies;
		return this.skillsCatalogue.map((sk) => ({
			id: sk.id,
			name: sk.name,
			baseDamageLabel: formatBaseDamage(sk.baseDamage),
			cooldownLabel: formatCooldown(sk.cooldownMs),
			usedByCount: enemies.filter((e) => e.skillPool.includes(sk.id)).length,
			rarityColor: rarityColor(sk.rarityId)
		}));
	});

	/* ── selected skill + dossier ─────────────────────────────────────────────────── */

	/** The inspected skill: an explicitly-requested id resolves even when retired (see the enemy tab's
	 *  `selectedEnemy`), falling back to the head of the catalogue otherwise. */
	readonly selectedSkill = $derived<ISkill | undefined>(
		(staticData.skills ?? [])[this.selectedSkillId] ?? this.skillsCatalogue[0]
	);

	/** The selected skill's rarity tier (hue + label) for the dossier header accent. */
	readonly selectedSkillRarity = $derived.by<SkillRarityVM | null>(() => {
		const sk = this.selectedSkill;
		return sk ? { color: rarityColor(sk.rarityId), label: rarityLabel(sk.rarityId) } : null;
	});

	/** The attributes the selected skill's damage scales with, each tinted by its attribute accent. */
	readonly skillScaling = $derived.by<SkillScalingVM[]>(() => {
		const sk = this.selectedSkill;
		if (!sk) {
			return [];
		}
		return sk.damageMultipliers.map((m) => ({
			attributeId: m.attributeId,
			name: attributeName(m.attributeId, staticData.attributes),
			code: attributeCode(m.attributeId, staticData.attributes),
			multiplierLabel: `×${formatNum(m.multiplier)}`,
			color: attributeColor(m.attributeId)
		}));
	});

	/** The selected skill's authored effects, described via the shared helper so the wording and the
	 *  buff/debuff direction match every other surface that shows an effect. */
	readonly skillEffects = $derived.by<SkillEffectVM[]>(() => {
		const sk = this.selectedSkill;
		if (!sk) {
			return [];
		}
		return sk.effects.map((effect) => {
			const desc = describeEffect(
				effect,
				attributeName(effect.attributeId, staticData.attributes),
				attributeIsHarmful(effect.attributeId, staticData.attributes)
			);
			return {
				id: effect.id,
				magnitude: desc.magnitude,
				attributeName: desc.attributeName,
				targetLabel: desc.targetLabel,
				duration: desc.duration,
				color: effectDirectionColor(desc.direction)
			};
		});
	});

	/** The (non-retired) enemies whose skill pool includes the selected skill — pills that cross-link
	 *  into the enemy dossier. Empty for a player-only skill. */
	readonly skillUsedBy = $derived.by<SkillUserVM[]>(() => {
		const sk = this.selectedSkill;
		if (!sk) {
			return [];
		}
		return this.enemies
			.filter((e) => e.skillPool.includes(sk.id))
			.map((e) => ({
				enemyId: e.id,
				name: e.name,
				isBoss: e.isBoss,
				accent: enemyAccent(e.isBoss)
			}));
	});

	/** How the selected skill is obtained — derived from actual item-grant references, with the
	 *  acquisition flag wording the no-source case. Item sources tint by the granting item's rarity. */
	readonly skillProvenance = $derived.by<SkillProvenanceVM>(() => {
		const sk = this.selectedSkill;
		if (!sk) {
			return { status: 'unobtainable', sources: [], emptyLabel: SKILL_ACQUISITION_EMPTY.unobtainable };
		}
		const allItems = staticData.items ?? [];
		const provenance = resolveSkillProvenance(
			sk,
			allItems.filter((i) => !i.retiredAt)
		);
		const sources = provenance.sources.map<SkillSourceVM>((src) => ({
			kind: src.kind,
			id: src.id,
			label: SKILL_SOURCE_LABEL,
			name: src.name,
			accent: rarityColor(allItems[src.id]?.rarityId ?? ERarity.Common)
		}));
		return {
			status: provenance.status,
			sources,
			emptyLabel: sources.length > 0 ? '' : SKILL_ACQUISITION_EMPTY[provenance.status]
		};
	});

	/** Every statistic that references the selected skill (the skill dossier's "your record"). */
	readonly skillStatistics = $derived.by<EntityStatVM[]>(() =>
		this.selectedSkill ? this.statVMsFor('skill', this.selectedSkill.id) : []
	);

	/* ── handlers ──────────────────────────────────────────────────────────────── */

	selectSkill(id: number): void {
		this.selectedSkillId = id;
	}

	/* ── helpers (store reads, kept off the reactive graph for the constructor) ──── */

	/** Resolve a skill id against the full catalogue (an explicit id resolves even when retired),
	 *  falling back to the head of the live catalogue. */
	private resolveSkill(id: number): ISkill | undefined {
		return (staticData.skills ?? [])[id] ?? liveSkills(staticData.skills)[0];
	}
}
