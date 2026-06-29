import { ELogType } from '$lib/api';
import type { LogMessage, LogOutcome, ResistOutcome } from '$lib/engine/log';

export type GlyphKind =
	| 'hit'
	| 'crit'
	| 'dodge'
	| 'block'
	| 'enemy'
	| 'loot'
	| 'system'
	| 'kill'
	| 'effect'
	| 'resisted'
	| 'vulnerable'
	| 'absorbed';

export interface LogKind {
	/** Accent color for the chip, message, and left bar. */
	color: string;
	glyph: GlyphKind;
	/** Short mono label for the chip (used as a title/aria hint). */
	label: string;
}

/**
 * The semantic combat-log palette. The actual colours live in the root CSS
 * variables (`--log-*`, defined in `+layout.svelte`) so they are themeable and
 * configurable in one place; these are just `var(...)` references. Reused by the
 * Options screen's log-type rows and live preview so both stay in sync with the
 * real log. Each value is valid wherever a colour is — SVG `stroke`, a CSS
 * `color`, or inside `color-mix(...)`.
 */
export const logColors = {
	player: 'var(--log-player)',
	enemy: 'var(--log-enemy)',
	loot: 'var(--log-loot)',
	reward: 'var(--log-reward)',
	system: 'var(--log-system)',
	effect: 'var(--log-effect)'
} as const;

const PLAYER = logColors.player;
const ENEMY = logColors.enemy;
const LOOT = logColors.loot;
const REWARD = logColors.reward;
const SYSTEM = logColors.system;
const EFFECT = logColors.effect;

/**
 * Visual treatment per structured combat outcome. The `Damage` channel carries player-vs-enemy and
 * the player-only crit/dodge/block outcomes (#178) on one `ELogType`; keying the glyph off the typed
 * {@link LogOutcome} the engine sets keeps it stable when a message is reworded.
 */
const damageKinds: Record<LogOutcome, LogKind> = {
	'player-hit': { color: PLAYER, glyph: 'hit', label: 'Hit' },
	'player-crit': { color: PLAYER, glyph: 'crit', label: 'Crit' },
	'player-dodge': { color: ENEMY, glyph: 'dodge', label: 'Dodge' },
	'player-block': { color: ENEMY, glyph: 'block', label: 'Block' },
	'enemy-hit': { color: ENEMY, glyph: 'enemy', label: 'Hurt' }
};

/**
 * Visual override for a typed hit's resist outcome (#1320, Area F): a `Damage` entry whose
 * {@link ResistOutcome} the engine set re-tags its glyph + chip label so resisted / vulnerable /
 * absorbed lines read at a glance. `resisted`/`vulnerable` keep the base hit's player/enemy colour (the
 * arrow direction conveys the rest); an `absorbed` hit became a heal, so it borrows the loot/heal hue.
 */
const resistKinds: Record<Exclude<ResistOutcome, 'normal'>, { glyph: GlyphKind; label: string; color?: string }> = {
	resisted: { glyph: 'resisted', label: 'Resist' },
	vulnerable: { glyph: 'vulnerable', label: 'Vuln' },
	absorbed: { glyph: 'absorbed', label: 'Absorb', color: LOOT }
};

/**
 * Maps a {@link LogMessage} to its visual treatment for the sliding manifest.
 *
 * `Damage` entries carry a structured {@link LogOutcome} (set by the battle engine at the log site),
 * so the glyph is chosen from that typed value rather than the message text. Entries without one
 * (e.g. the Options live-preview samples) and the `EnemyDefeated` channel still disambiguate
 * player-vs-enemy by the "You" message prefix the engine produces.
 */
export function logKind(log: LogMessage): LogKind {
	const fromPlayer = log.message.startsWith('You');
	switch (log.logType) {
		case ELogType.Damage: {
			// No structured outcome (e.g. live-preview samples): fall back to the "You" prefix split.
			const base = log.outcome
				? damageKinds[log.outcome]
				: fromPlayer
					? damageKinds['player-hit']
					: damageKinds['enemy-hit'];
			// A typed hit's resist outcome re-tags the glyph + label over the base hit treatment (#1320).
			if (log.resist && log.resist !== 'normal') {
				const resist = resistKinds[log.resist];
				return { color: resist.color ?? base.color, glyph: resist.glyph, label: resist.label };
			}
			return base;
		}
		case ELogType.ItemFound:
			return { color: LOOT, glyph: 'loot', label: 'Loot' };
		case ELogType.Exp:
			return { color: REWARD, glyph: 'crit', label: 'Exp' };
		case ELogType.LevelUp:
			return { color: REWARD, glyph: 'crit', label: 'Level' };
		case ELogType.Proficiency:
			return { color: REWARD, glyph: 'crit', label: 'Prof' };
		case ELogType.EnemyDefeated:
			return fromPlayer
				? { color: ENEMY, glyph: 'kill', label: 'Defeat' }
				: { color: LOOT, glyph: 'kill', label: 'Victory' };
		case ELogType.SkillEffect:
			return { color: EFFECT, glyph: 'effect', label: 'Effect' };
		case ELogType.Debug:
		default:
			return { color: SYSTEM, glyph: 'system', label: 'Info' };
	}
}

/** Formats an entry's wall-clock timestamp (epoch ms) as local 24-hour HH:MM:SS, so the panel
 *  shows the actual time each event occurred. */
export function formatLogTime(timestamp: number): string {
	const date = new Date(timestamp);
	const hours = String(date.getHours()).padStart(2, '0');
	const minutes = String(date.getMinutes()).padStart(2, '0');
	const seconds = String(date.getSeconds()).padStart(2, '0');
	return `${hours}:${minutes}:${seconds}`;
}
