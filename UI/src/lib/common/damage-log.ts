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
 * engine knows each hit's outcome explicitly (player vs enemy, crit/dodge) at the log site, so it
 * sets this rather than encoding the outcome only in the prose — letting `logKind` pick the glyph
 * from a typed value instead of sniffing the message text, which would silently drift on a reword.
 */
export type LogOutcome = 'player-hit' | 'player-crit' | 'player-dodge' | 'enemy-hit';

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
	outcome: LogOutcome,
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
