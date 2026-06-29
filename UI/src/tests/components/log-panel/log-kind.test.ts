import { describe, it, expect } from 'vitest';
import { ELogType } from '$lib/api';
import type { LogOutcome, ResistOutcome } from '$lib/engine/log';
import { logKind, formatLogTime, logColors } from '../../../components/log-panel/log-kind';

type LogMessage = {
	id: number;
	logType: ELogType;
	message: string;
	timestamp: number;
	outcome?: LogOutcome;
	resist?: ResistOutcome;
};

function makeLog(logType: ELogType, message: string, outcome?: LogOutcome, resist?: ResistOutcome): LogMessage {
	return { id: 1, logType, message, timestamp: 0, outcome, resist };
}

describe('logKind', () => {
	describe('ELogType.Damage', () => {
		// Glyph selection is driven by the structured `outcome`, not the message text — so the message
		// passed here is deliberately empty/mismatched to prove the prose is no longer load-bearing.
		it('returns the player-hit treatment for the "player-hit" outcome', () => {
			const result = logKind(makeLog(ELogType.Damage, '', 'player-hit'));
			expect(result.color).toBe(logColors.player);
			expect(result.glyph).toBe('hit');
			expect(result.label).toBe('Hit');
		});

		it('returns the enemy-hit treatment for the "enemy-hit" outcome', () => {
			const result = logKind(makeLog(ELogType.Damage, '', 'enemy-hit'));
			expect(result.color).toBe(logColors.enemy);
			expect(result.glyph).toBe('enemy');
			expect(result.label).toBe('Hurt');
		});

		it('returns the crit treatment for the "player-crit" outcome', () => {
			const result = logKind(makeLog(ELogType.Damage, '', 'player-crit'));
			expect(result.color).toBe(logColors.player);
			expect(result.glyph).toBe('crit');
			expect(result.label).toBe('Crit');
		});

		it('returns the dodge treatment for the "player-dodge" outcome', () => {
			const result = logKind(makeLog(ELogType.Damage, '', 'player-dodge'));
			expect(result.color).toBe(logColors.enemy);
			expect(result.glyph).toBe('dodge');
			expect(result.label).toBe('Dodge');
		});

		it('returns the reflect treatment in the player hue for the "player-reflect" outcome (#1330)', () => {
			const result = logKind(makeLog(ELogType.Damage, '', 'player-reflect'));
			expect(result.color).toBe(logColors.player);
			expect(result.glyph).toBe('reflect');
			expect(result.label).toBe('Reflect');
		});

		it('returns the reflect treatment in the enemy hue for the "enemy-reflect" outcome (#1330)', () => {
			const result = logKind(makeLog(ELogType.Damage, '', 'enemy-reflect'));
			expect(result.color).toBe(logColors.enemy);
			expect(result.glyph).toBe('reflect');
			expect(result.label).toBe('Reflect');
		});

		it('keys the glyph off the outcome even when the message reads like a different outcome', () => {
			// A reworded/misleading message must not flip the glyph: the outcome wins.
			const result = logKind(makeLog(ELogType.Damage, 'You landed a critical hit!', 'player-hit'));
			expect(result.glyph).toBe('hit');
			expect(result.glyph).not.toBe('crit');
		});

		describe('damage-type resist outcome (#1320)', () => {
			it('re-tags a resisted hit with the resist glyph and chip label, keeping the base hit colour', () => {
				const result = logKind(makeLog(ELogType.Damage, '', 'player-hit', 'resisted'));
				expect(result.glyph).toBe('resisted');
				expect(result.label).toBe('Resist');
				expect(result.color).toBe(logColors.player);
			});

			it('re-tags a vulnerable hit with the vulnerable glyph and chip label', () => {
				const result = logKind(makeLog(ELogType.Damage, '', 'enemy-hit', 'vulnerable'));
				expect(result.glyph).toBe('vulnerable');
				expect(result.label).toBe('Vuln');
				expect(result.color).toBe(logColors.enemy);
			});

			it('re-tags an absorbed hit with the absorb glyph, label, and heal hue', () => {
				const result = logKind(makeLog(ELogType.Damage, '', 'player-hit', 'absorbed'));
				expect(result.glyph).toBe('absorbed');
				expect(result.label).toBe('Absorb');
				expect(result.color).toBe(logColors.loot);
			});

			it('leaves the base hit treatment untouched for a normal (unresisted) outcome', () => {
				const result = logKind(makeLog(ELogType.Damage, '', 'player-hit', 'normal'));
				expect(result.glyph).toBe('hit');
				expect(result.label).toBe('Hit');
			});
		});

		describe('without a structured outcome (e.g. Options live-preview samples)', () => {
			it('falls back to the player-hit treatment when the message starts with "You"', () => {
				const result = logKind(makeLog(ELogType.Damage, 'You used Slash for 42 damage.'));
				expect(result.color).toBe(logColors.player);
				expect(result.glyph).toBe('hit');
				expect(result.label).toBe('Hit');
			});

			it('falls back to the enemy-hit treatment when the message does not start with "You"', () => {
				const result = logKind(makeLog(ELogType.Damage, 'Goblin hit you for 15 damage.'));
				expect(result.color).toBe(logColors.enemy);
				expect(result.glyph).toBe('enemy');
				expect(result.label).toBe('Hurt');
			});
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

	it('returns the reward treatment for ELogType.Proficiency', () => {
		const result = logKind(makeLog(ELogType.Proficiency, 'Fire Magic reached level 5'));
		expect(result.color).toBe(logColors.reward);
		expect(result.glyph).toBe('crit');
		expect(result.label).toBe('Prof');
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
	// Construct each instant from local-time components and read it back with the same local
	// getters, so the assertions are independent of the runner's timezone.
	const at = (h: number, m: number, s: number) => new Date(2026, 5, 21, h, m, s).getTime();

	it('formats an afternoon timestamp as local 24-hour HH:MM:SS', () => {
		expect(formatLogTime(at(14, 32, 7))).toBe('14:32:07');
	});

	it('pads single-digit hours, minutes, and seconds with a leading zero', () => {
		expect(formatLogTime(at(3, 5, 9))).toBe('03:05:09');
	});

	it('formats midnight as 00:00:00', () => {
		expect(formatLogTime(at(0, 0, 0))).toBe('00:00:00');
	});

	it('formats the last second of the day', () => {
		expect(formatLogTime(at(23, 59, 59))).toBe('23:59:59');
	});
});
