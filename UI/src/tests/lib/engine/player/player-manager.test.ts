import { describe, it, expect, vi, beforeEach } from 'vitest';
import { PlayerManager } from '$lib/engine/player/player-manager';
import { logMessage } from '$lib/engine/log';
import { EAttribute, ELogType, EModifierType } from '$lib/api';
import type { IPlayerData } from '$lib/api';
import { STAT_POINTS_PER_LEVEL } from '$lib/api/types/game-constants';

// PlayerManager only depends on logMessage; mock it so the unit under test is
// isolated and the spy assertions below work (mirrors inventory-manager.test.ts).
vi.mock('$lib/engine/log', () => ({
	logMessage: vi.fn()
}));

// Controllable stub for the network boundary unlockLesson/markLessonRead cross (mirrors
// inventory-manager.test.ts's socket stub); the assertions below only care that the command fires.
const { mockSendSocketCommand } = vi.hoisted(() => {
	const mockSendSocketCommand = vi.fn();
	return { mockSendSocketCommand };
});

vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return {
		...actual,
		apiSocket: {
			sendSocketCommand: mockSendSocketCommand
		}
	};
});

const makePlayerData = (overrides: Partial<IPlayerData> = {}): IPlayerData => ({
	id: 1,
	name: 'TestPlayer',
	level: 5,
	exp: 200,
	currentZone: 2,
	statPointsGained: 30,
	statPointsUsed: 24,
	attributes: [
		{ attributeId: EAttribute.Strength, amount: 10 },
		{ attributeId: EAttribute.Endurance, amount: 8 }
	],
	lockedBaseDistribution: [],
	signaturePassive: {
		attributeId: EAttribute.Strength,
		amount: 0,
		scalingAmount: 0,
		modifierType: EModifierType.Additive
	},
	unlockedSkills: [
		{ skillId: 0, selected: true, order: 1 },
		{ skillId: 1, selected: true, order: 0 },
		{ skillId: 2, selected: false },
		{ skillId: 3, selected: true, order: 2 }
	],
	logPreferences: [
		{ id: ELogType.Damage, enabled: false },
		{ id: ELogType.Exp, enabled: true }
	],
	lessons: [],
	inventoryData: {
		unlockedItems: [
			{
				itemId: 1,
				equipped: true,
				equipmentSlotId: 4,
				favorite: false,
				appliedMods: [{ itemModId: 10, itemModSlotId: 0 }]
			},
			{
				itemId: 2,
				equipped: false,
				favorite: false,
				appliedMods: []
			}
		],
		unlockedMods: [10, 11, 12]
	},
	playerRating: 100,
	...overrides
});

describe('PlayerManager', () => {
	let manager: PlayerManager;

	beforeEach(() => {
		manager = new PlayerManager();
		vi.mocked(logMessage).mockClear();
		mockSendSocketCommand.mockClear();
	});

	describe('initialize', () => {
		it('copies all fields from player data', () => {
			const data = makePlayerData();
			manager.initialize(data);

			expect(manager.name).toBe('TestPlayer');
			expect(manager.level).toBe(5);
			expect(manager.exp).toBe(200);
			expect(manager.currentZone).toBe(2);
			expect(manager.statPointsGained).toBe(30);
			expect(manager.statPointsUsed).toBe(24);
			expect(manager.attributes).toEqual(data.attributes);
			expect(manager.unlockedSkills).toEqual(data.unlockedSkills);
			expect(manager.logPreferences).toEqual(data.logPreferences);
			expect(manager.lessons).toEqual(data.lessons);
			expect(manager.inventoryData).toEqual(data.inventoryData);
		});

		it('derives selectedSkills as the equipped ids in loadout order', () => {
			manager.initialize(makePlayerData());

			// Equipped skills 1 (order 0), 0 (order 1), 3 (order 2); skill 2 is unequipped.
			expect(manager.selectedSkills).toEqual([1, 0, 3]);
		});

		it('overwrites previous state on re-initialize', () => {
			manager.initialize(makePlayerData({ name: 'First', level: 1 }));
			manager.initialize(makePlayerData({ name: 'Second', level: 10 }));

			expect(manager.name).toBe('Second');
			expect(manager.level).toBe(10);
		});
	});

	describe('setSelectedSkills', () => {
		it('replaces the loadout, updating each unlocked skill’s selected flag and slot order', () => {
			manager.initialize(makePlayerData());

			manager.setSelectedSkills([2, 0]);

			expect(manager.selectedSkills).toEqual([2, 0]);
			expect(manager.unlockedSkills.find((s) => s.skillId === 2)).toMatchObject({ selected: true, order: 0 });
			expect(manager.unlockedSkills.find((s) => s.skillId === 0)).toMatchObject({ selected: true, order: 1 });
			// A skill dropped from the loadout is unselected and loses its slot order.
			expect(manager.unlockedSkills.find((s) => s.skillId === 1)).toMatchObject({ selected: false, order: undefined });
		});

		it('memoizes selectedSkills, returning a stable reference until the loadout changes', () => {
			manager.initialize(makePlayerData());

			const first = manager.selectedSkills;
			// Re-reading without a mutation returns the same memoized array rather than re-deriving.
			expect(manager.selectedSkills).toBe(first);

			manager.setSelectedSkills([0]);

			expect(manager.selectedSkills).not.toBe(first);
			expect(manager.selectedSkills).toEqual([0]);
		});
	});

	describe('grantExp', () => {
		it('adds exp and logs the message', () => {
			manager.initialize(makePlayerData({ level: 1, exp: 0 }));
			manager.grantExp(50);

			expect(manager.exp).toBe(50);
			expect(logMessage).toHaveBeenCalledWith(ELogType.Exp, 'Earned 50 exp.');
		});

		it('triggers levelUp when exp exceeds threshold', () => {
			manager.initialize(makePlayerData({ level: 1, exp: 0 }));
			manager.grantExp(100);

			expect(manager.level).toBe(2);
			expect(manager.exp).toBe(0);
			expect(manager.statPointsGained).toBe(30 + STAT_POINTS_PER_LEVEL);
		});

		it('does not level up when exp is below threshold', () => {
			manager.initialize(makePlayerData({ level: 2, exp: 0 }));
			manager.grantExp(199);

			expect(manager.level).toBe(2);
			expect(manager.exp).toBe(199);
		});

		it('does not over-level from the pre-initialize level=0 default (threshold clamps to ≥ 1)', () => {
			// Before initialize, level defaults to 0; a 0 * EXP_PER_LEVEL threshold would otherwise
			// loop. The clamp keeps the threshold at one level's worth of exp.
			manager.grantExp(50);

			expect(manager.level).toBe(0);
			expect(manager.exp).toBe(50);
		});

		it('levels up exactly once past the clamped threshold from a level=0 default', () => {
			manager.grantExp(150);

			expect(manager.level).toBe(1);
			expect(manager.exp).toBe(50);
		});
	});

	describe('applyVictoryRewards', () => {
		it('grants exp and adopts the server-authoritative post-grant fields', () => {
			manager.initialize(makePlayerData({ level: 1, exp: 0, statPointsGained: 0, statPointsUsed: 0 }));

			manager.applyVictoryRewards({
				expReward: 100,
				newLevel: 2,
				newExp: 0,
				statPointsGained: 2,
				statPointsUsed: 0,
				playerRating: 150
			});

			expect(manager.level).toBe(2);
			expect(manager.exp).toBe(0);
			expect(manager.statPointsGained).toBe(2);
			expect(manager.playerRating).toBe(150);
			expect(logMessage).toHaveBeenCalledWith(ELogType.Exp, 'Earned 100 exp.');
			expect(logMessage).toHaveBeenCalledWith(ELogType.LevelUp, 'Congratulations, you leveled up!');
		});

		it('reconciles onto the server state even when the local recompute would drift (e.g. a clamped grant)', () => {
			// The backend clamps a single grant to MaxExpPerGrant (an anti-cheat backstop grantExp
			// deliberately doesn't mirror); the server's returned fields must win regardless.
			manager.initialize(makePlayerData({ level: 1, exp: 0, statPointsGained: 0, statPointsUsed: 5 }));

			manager.applyVictoryRewards({
				expReward: 1_000_000,
				newLevel: 3,
				newExp: 40,
				statPointsGained: 4,
				statPointsUsed: 5,
				playerRating: 200
			});

			expect(manager.level).toBe(3);
			expect(manager.exp).toBe(40);
			expect(manager.statPointsGained).toBe(4);
			expect(manager.statPointsUsed).toBe(5);
		});
	});

	describe('levelUp', () => {
		it('increments level and grants the per-level free pool stat points', () => {
			manager.initialize(makePlayerData({ level: 3, exp: 300, statPointsGained: 12 }));
			manager.levelUp();

			expect(manager.level).toBe(4);
			expect(manager.exp).toBe(0);
			expect(manager.statPointsGained).toBe(12 + STAT_POINTS_PER_LEVEL);
		});

		it('logs level-up messages', () => {
			manager.initialize(makePlayerData({ level: 1, exp: 100 }));
			manager.levelUp();

			expect(logMessage).toHaveBeenCalledWith(ELogType.LevelUp, 'Congratulations, you leveled up!');
			expect(logMessage).toHaveBeenCalledWith(ELogType.LevelUp, 'You are now level 2.');
		});
	});

	describe('battleLockedBaseModifiers', () => {
		const distribution = [
			{ attributeId: EAttribute.Strength, baseAmount: 10, amountPerLevel: 2 },
			{ attributeId: EAttribute.Endurance, baseAmount: 5, amountPerLevel: 0 }
		];

		it('composes the class locked base from the distribution at the current level', () => {
			manager.initialize(makePlayerData({ level: 3, lockedBaseDistribution: distribution }));

			const modifiers = manager.battleLockedBaseModifiers;

			// BaseAmount + AmountPerLevel × level: Strength = 10 + 2·3 = 16; Endurance = 5 + 0·3 = 5.
			expect(modifiers.map((m) => [m.attribute, m.amount])).toEqual([
				[EAttribute.Strength, 16],
				[EAttribute.Endurance, 5]
			]);
		});

		it('memoises a stable reference until the level changes, then recomputes', () => {
			manager.initialize(makePlayerData({ level: 3, exp: 0, lockedBaseDistribution: distribution }));

			const first = manager.battleLockedBaseModifiers;
			// Same level → same reference (the battle engine's by-reference change-detection relies on this).
			expect(manager.battleLockedBaseModifiers).toBe(first);

			manager.levelUp();
			const afterLevelUp = manager.battleLockedBaseModifiers;
			expect(afterLevelUp).not.toBe(first);
			// Strength rescales with the new level: 10 + 2·4 = 18.
			expect(afterLevelUp.find((m) => m.attribute === EAttribute.Strength)?.amount).toBe(18);
		});

		it('is empty for a class with no attribute distribution', () => {
			manager.initialize(makePlayerData({ lockedBaseDistribution: [] }));

			expect(manager.battleLockedBaseModifiers).toEqual([]);
		});
	});

	describe('addUnlockedSkill', () => {
		it('adds the skill to the unlocked set unselected and logs it', () => {
			manager.initialize(makePlayerData({ unlockedSkills: [{ skillId: 0, selected: true, order: 0 }] }));

			manager.addUnlockedSkill(5);

			expect(manager.unlockedSkills).toContainEqual({ skillId: 5, selected: false });
			// Earning a skill does not equip it.
			expect(manager.selectedSkills).not.toContain(5);
			expect(logMessage).toHaveBeenCalledWith(ELogType.ItemFound, 'New skill unlocked!');
		});

		it('is idempotent — an already-unlocked skill is not duplicated', () => {
			manager.initialize(makePlayerData({ unlockedSkills: [{ skillId: 5, selected: false }] }));

			manager.addUnlockedSkill(5);

			expect(manager.unlockedSkills.filter((s) => s.skillId === 5)).toHaveLength(1);
			expect(logMessage).not.toHaveBeenCalled();
		});
	});

	describe('logTypeEnabled', () => {
		it('returns the stored enabled flag for a known log type via the prebuilt map', () => {
			manager.initialize(makePlayerData());

			expect(manager.logTypeEnabled(ELogType.Damage)).toBe(false);
			expect(manager.logTypeEnabled(ELogType.Exp)).toBe(true);
		});

		it('defaults an unknown log type to enabled', () => {
			manager.initialize(makePlayerData({ logPreferences: [] }));

			expect(manager.logTypeEnabled(ELogType.LevelUp)).toBe(true);
		});

		it('rebuilds the lookup when preferences are reassigned', () => {
			manager.initialize(makePlayerData());
			expect(manager.logTypeEnabled(ELogType.Damage)).toBe(false);

			manager.logPreferences = [{ id: ELogType.Damage, enabled: true }];

			expect(manager.logTypeEnabled(ELogType.Damage)).toBe(true);
		});
	});

	describe('unlockLesson', () => {
		it('adds an unread lesson entry and calls the socket command', () => {
			manager.initialize(makePlayerData());

			manager.unlockLesson(3);

			const lesson = manager.lessons.find((l) => l.lessonId === 3);
			expect(lesson).toBeDefined();
			expect(lesson?.readAt).toBeUndefined();
			expect(mockSendSocketCommand).toHaveBeenCalledWith('UnlockLesson', 3);
		});

		it('is idempotent — an already-unlocked lesson is not duplicated or re-sent', () => {
			manager.initialize(makePlayerData({ lessons: [{ lessonId: 3, unlockedAt: '2026-01-01T00:00:00Z' }] }));

			manager.unlockLesson(3);

			expect(manager.lessons.filter((l) => l.lessonId === 3)).toHaveLength(1);
			expect(mockSendSocketCommand).not.toHaveBeenCalled();
		});
	});

	describe('markLessonRead', () => {
		it('sets readAt on a previously unlocked lesson and calls the socket command', () => {
			manager.initialize(makePlayerData({ lessons: [{ lessonId: 3, unlockedAt: '2026-01-01T00:00:00Z' }] }));

			manager.markLessonRead(3);

			expect(manager.lessons.find((l) => l.lessonId === 3)?.readAt).toBeDefined();
			expect(mockSendSocketCommand).toHaveBeenCalledWith('MarkLessonRead', 3);
		});

		it('normalizes a never-unlocked lesson straight to read (screen-anchored lessons skip unlock)', () => {
			manager.initialize(makePlayerData());

			manager.markLessonRead(3);

			const lesson = manager.lessons.find((l) => l.lessonId === 3);
			expect(lesson?.unlockedAt).toBeDefined();
			expect(lesson?.readAt).toBeDefined();
			expect(mockSendSocketCommand).toHaveBeenCalledWith('MarkLessonRead', 3);
		});

		it('is idempotent — an already-read lesson is not re-sent', () => {
			manager.initialize(
				makePlayerData({
					lessons: [{ lessonId: 3, unlockedAt: '2026-01-01T00:00:00Z', readAt: '2026-01-01T00:05:00Z' }]
				})
			);

			manager.markLessonRead(3);

			expect(mockSendSocketCommand).not.toHaveBeenCalled();
		});
	});

	describe('default state', () => {
		it('starts with empty defaults before initialization', () => {
			expect(manager.name).toBe('');
			expect(manager.level).toBe(0);
			expect(manager.exp).toBe(0);
			expect(manager.attributes).toEqual([]);
			expect(manager.unlockedSkills).toEqual([]);
			expect(manager.selectedSkills).toEqual([]);
			expect(manager.logPreferences).toEqual([]);
			expect(manager.lessons).toEqual([]);
			expect(manager.inventoryData).toEqual({
				unlockedItems: [],
				unlockedMods: []
			});
		});
	});
});
