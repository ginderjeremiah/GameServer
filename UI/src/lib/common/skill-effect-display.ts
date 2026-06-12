import { EAttribute, EModifierType, ESkillEffectTarget, type ISkillEffect } from '$lib/api';
import { formatNum } from './functions';

/*
 * Display helpers for authored skill effects (the timed buffs/debuffs a skill applies on fire).
 * Pure derivation only — the visual surfaces (the skill-button badge, the skill tooltip's effect
 * lines, and the battler-card active-effect chips) consume these so the wording and buff/debuff
 * direction stay consistent across every place an effect is shown. The accent colours are themeable
 * `--effect-*` custom properties (declared in `+layout.svelte`), referenced here rather than
 * hard-coded, mirroring the rarity/challenge-type helpers.
 */

/** Attributes whose *increase* is detrimental to the battler carrying them — a positive modifier on
 *  one is a debuff and a negative one a buff. Every other attribute is beneficial when raised
 *  (HealthRegenPerSecond included), so only damage-over-second is inverted today. */
const HARMFUL_WHEN_RAISED: ReadonlySet<EAttribute> = new Set([EAttribute.DamageTakenPerSecond]);

export type EffectDirection = 'buff' | 'debuff';

/** Whether a modifier raises (`true`) or lowers (`false`) the attribute it targets: an additive
 *  amount raises when positive; a multiplicative factor raises when it exceeds 1. */
export const effectRaisesAttribute = (modifierType: EModifierType, amount: number): boolean =>
	modifierType === EModifierType.Multiplicative ? amount > 1 : amount > 0;

/** Classifies an effect as a buff or debuff *for the battler it lands on*: raising a beneficial
 *  attribute (or lowering a harmful one) is a buff; the inverse is a debuff. */
export const effectDirection = (
	attribute: EAttribute,
	modifierType: EModifierType,
	amount: number
): EffectDirection => {
	const raises = effectRaisesAttribute(modifierType, amount);
	const beneficial = HARMFUL_WHEN_RAISED.has(attribute) ? !raises : raises;
	return beneficial ? 'buff' : 'debuff';
};

/** The magnitude badge for an effect: a signed delta for additive amounts (`+15` / `-15`) or a
 *  `×factor` for multiplicative ones (`×1.5` / `×0.5`). */
export const formatEffectMagnitude = (modifierType: EModifierType, amount: number): string => {
	if (modifierType === EModifierType.Multiplicative) {
		return `×${formatNum(amount)}`;
	}
	return `${amount < 0 ? '-' : '+'}${formatNum(Math.abs(amount))}`;
};

/** A duration in seconds, e.g. `5s` or `2.5s`. */
export const formatEffectDuration = (durationMs: number): string => `${formatNum(durationMs / 1000)}s`;

/** The side an effect lands on, from the casting skill's perspective. */
export const effectTargetLabel = (target: ESkillEffectTarget): string =>
	target === ESkillEffectTarget.Self ? 'self' : 'enemy';

/** The themeable accent for an effect direction (a CSS `var(...)`, overridable by themes). */
export const effectDirectionColor = (direction: EffectDirection): string =>
	direction === 'buff' ? 'var(--effect-buff)' : 'var(--effect-debuff)';

export interface EffectDescription {
	direction: EffectDirection;
	/** Signed/`×` magnitude badge, e.g. `+15` or `×0.5`. */
	magnitude: string;
	/** The affected attribute's display name, as supplied to {@link describeEffect}. */
	attributeName: string;
	targetLabel: string;
	/** Duration in seconds, e.g. `5s`. */
	duration: string;
	/** Full one-line summary, e.g. `+15 Strength (self), 5s`. */
	text: string;
}

/** Builds the display pieces (and assembled one-line summary) for an authored skill effect. The
 *  attribute's display name is supplied by the caller so this stays pure — name resolution reads
 *  the reference-data store, which is the component's concern, not this helper's. */
export const describeEffect = (effect: ISkillEffect, attributeName: string): EffectDescription => {
	const direction = effectDirection(effect.attributeId, effect.modifierTypeId, effect.amount);
	const magnitude = formatEffectMagnitude(effect.modifierTypeId, effect.amount);
	const targetLabel = effectTargetLabel(effect.target);
	const duration = formatEffectDuration(effect.durationMs);
	return {
		direction,
		magnitude,
		attributeName,
		targetLabel,
		duration,
		text: `${magnitude} ${attributeName} (${targetLabel}), ${duration}`
	};
};

/** A combat-log line announcing a freshly-applied effect, phrased from the player's perspective so
 *  `logKind` tints it correctly (player lines lead with "You"). The wording reuses the same
 *  magnitude/duration pieces as the tooltip/chips, classifying the effect as empowering (buff) or
 *  weakening (debuff) *for the battler it lands on* — e.g. `You are empowered: +15 Strength for 5s`
 *  or `Goblin is weakened: -10 Defense for 5s`. */
export const effectLogMessage = (
	effect: ISkillEffect,
	attributeName: string,
	onPlayer: boolean,
	enemyName: string
): string => {
	const { direction, magnitude, duration } = describeEffect(effect, attributeName);
	const subject = onPlayer ? 'You are' : `${enemyName} is`;
	const verb = direction === 'buff' ? 'empowered' : 'weakened';
	return `${subject} ${verb}: ${magnitude} ${attributeName} for ${duration}`;
};
