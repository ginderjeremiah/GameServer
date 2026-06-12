import { describe, it, expect } from 'vitest';
import { ELogType } from '$lib/api';
import { logKind, formatLogTime, logColors } from '../../../components/log-panel/log-kind';

type LogMessage = { id: number; logType: ELogType; message: string };

function makeLog(logType: ELogType, message: string): LogMessage {
	return { id: 1, logType, message };
}

describe('logKind', () => {
	describe('ELogType.Damage', () => {
		it('returns the player-hit treatment when the message starts with "You"', () => {
			const result = logKind(makeLog(ELogType.Damage, 'You used Slash for 42 damage.'));
			expect(result.color).toBe(logColors.player);
			expect(result.glyph).toBe('hit');
			expect(result.label).toBe('Hit');
		});

		it('returns the enemy-hit treatment when the message does not start with "You"', () => {
			const result = logKind(makeLog(ELogType.Damage, 'Goblin hit you for 15 damage.'));
			expect(result.color).toBe(logColors.enemy);
			expect(result.glyph).toBe('enemy');
			expect(result.label).toBe('Hurt');
		});
	});

	describe('ELogType.EnemyDefeated', () => {
		it('returns the player-kill treatment when the message starts with "You"', () => {
			const result = logKind(makeLog(ELogType.EnemyDefeated, "You've defeated the Goblin!"));
			expect(result.color).toBe(logColors.enemy);
			expect(result.glyph).toBe('kill');
			expect(result.label).toBe('Defeat');
		});

		it('returns the enemy-victory treatment when the message does not start with "You"', () => {
			const result = logKind(makeLog(ELogType.EnemyDefeated, 'Goblin has defeated you!'));
			expect(result.color).toBe(logColors.loot);
			expect(result.glyph).toBe('kill');
			expect(result.label).toBe('Victory');
		});
	});

	it('returns the loot treatment for ELogType.ItemFound', () => {
		const result = logKind(makeLog(ELogType.ItemFound, 'Found Iron Sword!'));
		expect(result.color).toBe(logColors.loot);
		expect(result.glyph).toBe('loot');
		expect(result.label).toBe('Loot');
	});

	it('returns the exp reward treatment for ELogType.Exp', () => {
		const result = logKind(makeLog(ELogType.Exp, 'Gained 120 exp.'));
		expect(result.color).toBe(logColors.reward);
		expect(result.glyph).toBe('crit');
		expect(result.label).toBe('Exp');
	});

	it('returns the level reward treatment for ELogType.LevelUp', () => {
		const result = logKind(makeLog(ELogType.LevelUp, 'Level up! You are now level 5.'));
		expect(result.color).toBe(logColors.reward);
		expect(result.glyph).toBe('crit');
		expect(result.label).toBe('Level');
	});

	it('returns the effect treatment for ELogType.SkillEffect', () => {
		const result = logKind(makeLog(ELogType.SkillEffect, 'You are empowered: +15 Strength for 5s'));
		expect(result.color).toBe(logColors.effect);
		expect(result.glyph).toBe('effect');
		expect(result.label).toBe('Effect');
	});

	it('uses the same effect treatment for an enemy-directed effect line', () => {
		const result = logKind(makeLog(ELogType.SkillEffect, 'Goblin took 12 damage over time.'));
		expect(result.color).toBe(logColors.effect);
		expect(result.glyph).toBe('effect');
		expect(result.label).toBe('Effect');
	});

	it('returns the system treatment for ELogType.Debug', () => {
		const result = logKind(makeLog(ELogType.Debug, 'Debug info.'));
		expect(result.color).toBe(logColors.system);
		expect(result.glyph).toBe('system');
		expect(result.label).toBe('Info');
	});

	it('falls back to the system treatment for unknown log types', () => {
		const result = logKind(makeLog(999 as ELogType, 'Unknown event.'));
		expect(result.color).toBe(logColors.system);
		expect(result.glyph).toBe('system');
		expect(result.label).toBe('Info');
	});
});

describe('formatLogTime', () => {
	it('formats id 0 as 00:00', () => {
		expect(formatLogTime(0)).toBe('00:00');
	});

	it('ids below 60 still show 00:00 (sub-second resolution rounds down)', () => {
		expect(formatLogTime(30)).toBe('00:00');
		expect(formatLogTime(59)).toBe('00:00');
	});

	it('formats exactly one second', () => {
		expect(formatLogTime(60)).toBe('00:01');
	});

	it('formats one minute boundary', () => {
		expect(formatLogTime(3600)).toBe('01:00');
	});

	it('formats a value with both minutes and seconds', () => {
		// 90 seconds = 1 min 30 sec → id = 90 * 60 = 5400
		expect(formatLogTime(5400)).toBe('01:30');
	});

	it('pads single-digit minutes and seconds with a leading zero', () => {
		// 5 min 9 sec → id = 309 * 60 = 18540
		expect(formatLogTime(18540)).toBe('05:09');
	});

	it('wraps minutes at 100 (minutes % 100)', () => {
		// 100 minutes → id = 6000 * 60 = 360000
		expect(formatLogTime(360000)).toBe('00:00');
	});

	it('handles large ids correctly', () => {
		// 99 min 59 sec → id = (99*60 + 59) * 60 = 359940
		expect(formatLogTime(359940)).toBe('99:59');
	});
});
