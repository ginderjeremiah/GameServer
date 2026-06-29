import { EDamageType } from '$lib/api';
import { formatNum } from './functions';
import { damageTypeName } from './damage-type-display';

/*
 * Pure builders for the direct-hit combat-log line and its damage-type / resist feedback (#1320,
 * Area F). Kept store-free and side-effect-free (the caller passes the enemy name, mirroring
 * `effectLogMessage`) so the wording is unit-testable independent of the live battle engine.
 *
 * `LogOutcome` lives here — rather than in `$lib/engine/log` — so this module can type its message
 * builder off it without `$lib/common` depending on `$lib/engine` (which already depends on
 * `$lib/common`); `$lib/engine/log` re-exports it for its existing importers.
 */

/**
 * Structured combat-outcome discriminator carried alongside a `Damage`-channel entry. The battle
 * engine knows each hit's outcome explicitly (player vs enemy, crit/dodge, and the #1330 reflection
 * lines) at the log site, so it sets this rather than encoding the outcome only in the prose —
 * letting `logKind` pick the glyph from a typed value instead of sniffing the message text, which
 * would silently drift on a reword.
 */
export type LogOutcome =
	| 'player-hit'
	| 'player-crit'
	| 'player-dodge'
	| 'enemy-hit'
	| 'player-reflect'
	| 'enemy-reflect';

/** The two deterministic-reflection outcomes (#1330): `player-reflect` when the player returned a share
 *  of an incoming hit to the enemy, `enemy-reflect` when the enemy reflected onto the player. They carry
 *  their own prose ({@link reflectLogMessage}) and a distinct glyph, separate from the direct-hit lines. */
export type ReflectOutcome = Extract<LogOutcome, 'player-reflect' | 'enemy-reflect'>;

/** The direct-hit subset of {@link LogOutcome} (everything but reflection) — the only outcomes
 *  {@link damageLogMessage} phrases. Reflection lines use {@link reflectLogMessage} instead. */
export type DirectHitOutcome = Exclude<LogOutcome, ReflectOutcome>;

/**
 * How a hit's damage-type resistance resolved, for the combat-log feedback (#1320, Area F):
 * `resisted` (the defender took less), `vulnerable` (negative resistance — the defender took more),
 * `absorbed` (resistance ≥ 1 turned the hit into a net heal), or `normal` (no typed resistance in play).
 */
export type ResistOutcome = 'normal' | 'resisted' | 'vulnerable' | 'absorbed';

/** Classifies a hit's resist outcome from the defender's total resistance to its type and the net
 *  damage dealt. A net heal (`netDamage < 0`, only reachable at resistance ≥ 1) is `absorbed`; otherwise
 *  negative resistance is `vulnerable`, positive resistance is `resisted` (including a hit fully negated
 *  to 0 with no heal room), and exactly zero is `normal`. */
export const classifyResist = (resistance: number, netDamage: number): ResistOutcome => {
	if (netDamage < 0) {
		return 'absorbed';
	}
	if (resistance < 0) {
		return 'vulnerable';
	}
	if (resistance > 0) {
		return 'resisted';
	}
	return 'normal';
};

/** The leaf type's name as a lowercase prose word with a trailing space (e.g. `fire `), or empty for
 *  Physical so a basic attack reads `dealt 12 damage` rather than `dealt 12 physical damage`. */
const typeWord = (damageType: EDamageType): string =>
	damageType === EDamageType.Physical ? '' : `${damageTypeName(damageType).toLowerCase()} `;

/** The resist clause + terminal punctuation appended to a hit line: a plain `!` when normal, else a
 *  short em-dash note. Absorption is phrased separately (it flips the hit into a heal). */
const resistSuffix = (resist: ResistOutcome): string => {
	switch (resist) {
		case 'resisted':
			return ' — resisted.';
		case 'vulnerable':
			return ' — vulnerable!';
		default:
			return '!';
	}
};

/** The combat-log line for one direct hit, surfacing its damage type and resist outcome (#1320). The
 *  enemy name is supplied by the caller (store-free, like `effectLogMessage`). Absorption is reported
 *  as the defender recovering health, since the hit dealt a net heal (`damage` is negative). */
export const damageLogMessage = (
	skillName: string,
	damage: number,
	outcome: DirectHitOutcome,
	damageType: EDamageType,
	resist: ResistOutcome,
	enemyName: string
): string => {
	if (outcome === 'player-dodge') {
		return `You dodged ${enemyName}'s ${skillName}!`;
	}

	const word = typeWord(damageType);

	if (resist === 'absorbed') {
		const heal = formatNum(Math.max(-damage, 0));
		return outcome === 'enemy-hit'
			? `You absorbed ${enemyName}'s ${skillName}, recovering ${heal} health!`
			: `${enemyName} absorbed your ${skillName}, recovering ${heal} health!`;
	}

	const amount = formatNum(damage);
	const suffix = resistSuffix(resist);
	switch (outcome) {
		case 'player-crit':
			return `You landed a critical hit with ${skillName} for ${amount} ${word}damage${suffix}`;
		case 'player-hit':
			return `You used ${skillName} and dealt ${amount} ${word}damage${suffix}`;
		case 'enemy-hit':
			return `${enemyName} used ${skillName} and dealt ${amount} ${word}damage${suffix}`;
	}
};

/** The combat-log line for a deterministic damage-reflection event (#1330): the defender returned `amount`
 *  of a direct hit's net damage to the attacker, bypassing the attacker's mitigation. `player-reflect` is
 *  the player returning damage to the enemy; `enemy-reflect` is the enemy reflecting onto the player. Reflected
 *  damage is raw and untyped, so the line carries no damage-type/resist note. Store-free (the enemy name is
 *  supplied), mirroring {@link damageLogMessage}. */
export const reflectLogMessage = (outcome: ReflectOutcome, amount: number, enemyName: string): string => {
	const reflected = formatNum(amount);
	return outcome === 'player-reflect'
		? `You reflected ${reflected} damage back to ${enemyName}!`
		: `${enemyName} reflected ${reflected} damage back at you!`;
};
