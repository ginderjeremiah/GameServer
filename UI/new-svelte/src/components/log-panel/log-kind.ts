import { ELogType } from '$lib/api';
import type { LogMessage } from '$lib/engine/log';

export type GlyphKind = 'hit' | 'crit' | 'enemy' | 'loot' | 'system' | 'kill';

export interface LogKind {
	/** Accent color for the chip, message, and left bar. */
	color: string;
	glyph: GlyphKind;
	/** Short mono label for the chip (used as a title/aria hint). */
	label: string;
}

/**
 * The semantic combat-log palette — the single source of truth for the colours
 * used to accent log messages (also reused by the Options screen's log-type
 * rows and live preview so both stay in sync with the real log).
 */
export const logColors = {
	player: '#c0d8ff',
	enemy: '#e8b6a6',
	loot: '#bde0b4',
	reward: '#f0d28a',
	system: 'rgba(240, 240, 240, 0.7)'
} as const;

const PLAYER = logColors.player;
const ENEMY = logColors.enemy;
const LOOT = logColors.loot;
const REWARD = logColors.reward;
const SYSTEM = logColors.system;

/**
 * Maps a {@link LogMessage} to its visual treatment for the sliding manifest.
 *
 * The log model only stores a flat `message` + `logType`, so player-vs-enemy
 * actions (both `ELogType.Damage`) are disambiguated by the message prefix the
 * battle engine produces ("You used …" / "You've been defeated!").
 */
export function logKind(log: LogMessage): LogKind {
	const fromPlayer = log.message.startsWith('You');
	switch (log.logType) {
		case ELogType.Damage:
			return fromPlayer
				? { color: PLAYER, glyph: 'hit', label: 'Hit' }
				: { color: ENEMY, glyph: 'enemy', label: 'Hurt' };
		case ELogType.ItemFound:
			return { color: LOOT, glyph: 'loot', label: 'Loot' };
		case ELogType.Exp:
			return { color: REWARD, glyph: 'crit', label: 'Exp' };
		case ELogType.LevelUp:
			return { color: REWARD, glyph: 'crit', label: 'Level' };
		case ELogType.EnemyDefeated:
			return fromPlayer
				? { color: ENEMY, glyph: 'kill', label: 'Defeat' }
				: { color: LOOT, glyph: 'kill', label: 'Victory' };
		case ELogType.Debug:
		default:
			return { color: SYSTEM, glyph: 'system', label: 'Info' };
	}
}

/** Treats the monotonically-increasing log id as an elapsed-time clock. */
export function formatLogTime(id: number): string {
	const totalSeconds = Math.floor(id / 60);
	const minutes = Math.floor(totalSeconds / 60);
	const seconds = totalSeconds % 60;
	return `${String(minutes % 100).padStart(2, '0')}:${String(seconds).padStart(2, '0')}`;
}
