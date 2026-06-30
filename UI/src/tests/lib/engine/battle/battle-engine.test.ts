import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
	EDamageType,
	ERarity,
	EAttribute,
	ELogType,
	EModifierType,
	ESkillAcquisition,
	ESkillEffectTarget
} from '$lib/api';
import type { ISkill, IEnemy, IEnemyInstance } from '$lib/api';

// Callbacks captured from the mocked engine hooks. `onLogicalUpdate` emits a delta,
// `onRenderUpdate` a (renderDelta, logicalDelta) pair, and `onNewEnemyLoaded` an enemy instance.
type DeltaCallback = (delta: number) => void;
type RenderCallback = (renderDelta: number, logicalDelta: number) => void;
type EnemyLoadedCallback = (enemy: IEnemyInstance) => void;

vi.mock('svelte', async (importOriginal) => ({
	...((await importOriginal()) as Record<string, unknown>),
	onDestroy: vi.fn()
}));

const { mockSkills, mockEnemies, mockAttributes, mockPlayerManager, mockInventoryManager, mockPlayerProficiencies } =
	vi.hoisted(() => {
		const mockSkills: ISkill[] = [];
		const mockEnemies: IEnemy[] = [];
		const mockAttributes: { id: number; name: string }[] = [{ id: 0, name: 'Strength' }];
		const mockPlayerManager = {
			name: 'TestPlayer',
			level: 5,
			selectedSkills: [0],
			attributes: [
				{ attributeId: 0, amount: 50 },
				{ attributeId: 1, amount: 30 }
			],
			// The class locked-base battle modifiers — a stable reference (the real manager memoises it) the
			// battle engine compares by identity, so reassigning it simulates a class/level change.
			battleLockedBaseModifiers: [] as unknown[],
			// The class signature passive modifier, resolved against the assembled battler attributes and added
			// last at the reset seam. A flat no-op (amount 0) here so it doesn't perturb the attribute assertions.
			// Raw numeric enum values (hoisted mock runs before imports): type 1 = EModifierType.Additive,
			// source 9 = EAttributeModifierSource.Class — a flat additive 0 leaves every attribute untouched.
			// Ignores its scaling-resolver arg, so it's declared param-less (JS tolerates the extra call arg).
			battleSignaturePassiveModifier: () => ({
				attribute: 0,
				amount: 0,
				type: 1,
				source: 9
			})
		};
		const mockInventoryManager = {
			equipmentStats: [] as { attributeId: number; amount: number }[],
			grantedSkillIds: [] as number[],
			// The weapon-match gate's equipped weapon type (#1342), threaded to the player battler rebuild.
			// Literal EDamageType.Unarmed (13) rather than the enum: this object lives in a hoisted vi.mock
			// factory, where referencing the imported enum value at runtime throws (used before initialization).
			equippedWeaponType: 13 as EDamageType
		};
		// The player's proficiency battle modifiers — a stable reference (the real store memoises via a
		// `$derived`) the battle engine compares by identity, so reassigning it simulates a proficiency change.
		const mockPlayerProficiencies = { battleModifiers: [] as unknown[] };

		return {
			mockSkills,
			mockEnemies,
			mockAttributes,
			mockPlayerManager,
			mockInventoryManager,
			mockPlayerProficiencies
		};
	});

let { logicalUpdateCallbacks, renderUpdateCallbacks, enemyLoadedCallbacks } = vi.hoisted(() => {
	const logicalUpdateCallbacks: DeltaCallback[] = [];
	const renderUpdateCallbacks: RenderCallback[] = [];
	const enemyLoadedCallbacks: EnemyLoadedCallback[] = [];

	return { logicalUpdateCallbacks, renderUpdateCallbacks, enemyLoadedCallbacks };
});

vi.mock('$stores', () => ({
	staticData: {
		get skills() {
			return mockSkills;
		},
		get enemies() {
			return mockEnemies;
		},
		get attributes() {
			return mockAttributes;
		}
	},
	playerProficiencies: {
		get battleModifiers() {
			return mockPlayerProficiencies.battleModifiers;
		}
	}
}));

vi.mock('$lib/engine/log', () => ({
	logMessage: vi.fn()
}));

vi.mock('$lib/engine/engine', () => ({
	get inventoryManager() {
		return mockInventoryManager;
	}
}));

vi.mock('$lib/engine/player/player-manager', () => ({
	get playerManager() {
		return mockPlayerManager;
	}
}));

vi.mock('$lib/engine/logical-engine', () => ({
	// Mirrors the real exported constant; BattleSimulator reads it at module load for its
	// default max-duration cap. The engine itself is otherwise fully stubbed below.
	tickSize: 40,
	LogicalEngine: vi.fn(class {}),
	onLogicalUpdate: vi.fn((cb: DeltaCallback) => {
		logicalUpdateCallbacks.push(cb);
		return () => {
			logicalUpdateCallbacks = logicalUpdateCallbacks.filter((c) => c !== cb);
		};
	})
}));

vi.mock('$lib/engine/render-engine', () => ({
	RenderEngine: vi.fn(class {}),
	onRenderUpdate: vi.fn((cb: RenderCallback) => {
		renderUpdateCallbacks.push(cb);
		return () => {
			renderUpdateCallbacks = renderUpdateCallbacks.filter((c) => c !== cb);
		};
	})
}));

vi.mock('$lib/engine/battle/enemy-manager', () => ({
	onNewEnemyLoaded: vi.fn((cb: EnemyLoadedCallback) => {
		enemyLoadedCallbacks.push(cb);
		return () => {
			enemyLoadedCallbacks = enemyLoadedCallbacks.filter((c) => c !== cb);
		};
	})
}));

import { BattleEngine, BattleStage, onCombatFloat, type CombatFloatEvent } from '$lib/engine/battle/battle-engine';
import { logMessage } from '$lib/engine/log';
import { DEFAULT_MAX_BATTLE_MS } from '$lib/api/types/game-constants';

describe('BattleEngine', () => {
	let engine: BattleEngine;

	beforeEach(() => {
		logicalUpdateCallbacks = [];
		renderUpdateCallbacks = [];
		enemyLoadedCallbacks = [];
		vi.mocked(logMessage).mockClear();

		mockSkills.length = 0;
		mockSkills[0] = {
			id: 0,
			name: 'Slash',
			baseDamage: 100,
			damageMultipliers: [],
			effects: [],
			description: '',
			cooldownMs: 500,
			iconPath: '',
			rarityId: ERarity.Common,
			word: '',
			pronunciation: '',
			translation: '',
			damagePortions: [{ type: EDamageType.Physical, weight: 1 }],
			acquisition: ESkillAcquisition.Player
		};

		mockEnemies.length = 0;
		mockEnemies[1] = { id: 1, name: 'Goblin', isBoss: false, attributeDistribution: [], skillPool: [0], spawns: [] };

		engine = new BattleEngine();
	});

	describe('start/stop lifecycle', () => {
		it('starts in Idle stage', () => {
			expect(engine.stage).toBe(BattleStage.Idle);
		});

		it('registers hooks on start', () => {
			engine.start();

			expect(logicalUpdateCallbacks).toHaveLength(1);
			expect(renderUpdateCallbacks).toHaveLength(1);
			expect(enemyLoadedCallbacks).toHaveLength(1);
		});

		it('removes hooks on stop', () => {
			engine.start();
			engine.stop();

			expect(logicalUpdateCallbacks).toHaveLength(0);
			expect(renderUpdateCallbacks).toHaveLength(0);
			expect(enemyLoadedCallbacks).toHaveLength(0);
		});

		it('start is idempotent', () => {
			engine.start();
			engine.start();

			expect(logicalUpdateCallbacks).toHaveLength(1);
		});

		it('sets running flag', () => {
			expect(engine.running).toBe(false);
			engine.start();
			expect(engine.running).toBe(true);
			engine.stop();
			expect(engine.running).toBe(false);
		});

		it('resets the stage to Idle on stop so a restart begins from a clean baseline (#881)', () => {
			engine.start();
			// Park in the post-victory Loading cooldown — the stage a navigation to the admin screen most
			// often interrupts. Without the reset it would stay Loading, stranding the fight on the next start().
			engine.startLoading(1000);
			expect(engine.stage).toBe(BattleStage.Loading);

			engine.stop();

			expect(engine.stage).toBe(BattleStage.Idle);
		});

		it('resets a Paused stage to Idle on stop (a navigation mid boss-swap)', () => {
			engine.start();
			engine.pause();
			expect(engine.stage).toBe(BattleStage.Paused);

			engine.stop();

			expect(engine.stage).toBe(BattleStage.Idle);
		});
	});

	describe('battle state transitions', () => {
		it('transitions to Active when enemy is loaded', () => {
			engine.start();

			const enemyInstance = { id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);

			expect(engine.stage).toBe(BattleStage.Active);
		});

		it('transitions to Victorious when enemy dies', () => {
			engine.start();

			const enemyInstance = { id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);

			// Simulate enough logical updates to kill the enemy
			for (let i = 0; i < 100; i++) {
				if (engine.stage === BattleStage.Active) {
					logicalUpdateCallbacks[0](40);
				}
				if (engine.stage !== BattleStage.Active) break;
			}

			expect(engine.stage).toBe(BattleStage.Victorious);
		});

		it('pause sets stage to Paused', () => {
			engine.start();
			const enemyInstance = { id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);

			engine.pause();
			expect(engine.stage).toBe(BattleStage.Paused);
		});

		it('resume returns to Active when both alive', () => {
			engine.start();
			const enemyInstance = { id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);

			engine.pause();
			engine.resume();
			expect(engine.stage).toBe(BattleStage.Active);
		});

		it('resume falls back to Idle when a battler is already dead', () => {
			engine.start();
			const enemyInstance = { id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);

			engine.enemy.takeDamage(1e9, EDamageType.Physical, 1);
			engine.resume();

			expect(engine.stage).toBe(BattleStage.Idle);
		});

		it('transitions to Defeated and logs when the player dies', () => {
			engine.start();
			const enemyInstance = { id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);

			// Kill the player, then run a sub-cooldown tick so no skill fires and the enemy survives —
			// leaving the player-dead branch as the only outcome.
			engine.player.takeDamage(1e9, EDamageType.Physical, 1);
			logicalUpdateCallbacks[0](40);

			expect(engine.stage).toBe(BattleStage.Defeated);
			expect(logMessage).toHaveBeenCalledWith(ELogType.EnemyDefeated, "You've been defeated!");
		});

		it('ends the battle as a draw (TIMEOUT) when the time limit is reached with both battlers alive', () => {
			// A true stalemate: neither side deals damage, so nobody can ever land the killing blow.
			mockSkills[0].baseDamage = 0;
			engine.start();
			const enemyInstance = { id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);

			// Just short of the 2-minute cap: the battle is still in progress.
			logicalUpdateCallbacks[0](DEFAULT_MAX_BATTLE_MS - 40);
			expect(engine.stage).toBe(BattleStage.Active);

			// Crossing the cap with both battlers alive ends the fight as a draw, not a win or a loss.
			logicalUpdateCallbacks[0](40);
			expect(engine.stage).toBe(BattleStage.Drawn);
			expect(logMessage).toHaveBeenCalledWith(ELogType.EnemyDefeated, expect.stringContaining('draw'));
		});

		it('does not draw when the enemy dies on the same tick the cap is reached (death wins)', () => {
			engine.start();
			const enemyInstance = { id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);

			// Pre-kill the enemy, then cross the cap in one tick: the death branch is checked before the
			// timeout, so the outcome is a victory rather than a draw.
			engine.enemy.takeDamage(1e9, EDamageType.Physical, 1);
			logicalUpdateCallbacks[0](DEFAULT_MAX_BATTLE_MS);

			expect(engine.stage).toBe(BattleStage.Victorious);
		});
	});

	describe('startLoading', () => {
		it('enters the Loading stage and seeds the countdown', () => {
			engine.startLoading(100);

			expect(engine.stage).toBe(BattleStage.Loading);
			expect(engine.loadingTime).toBe(100);
			// loadingTotal captures the full duration so the cooldown UI can render the remaining fraction.
			expect(engine.loadingTotal).toBe(100);
		});

		it('counts the loading time down each logical tick and resolves + unhooks at zero', async () => {
			// The countdown is driven by the logical clock (not rAF), so it keeps advancing in a
			// backgrounded tab instead of freezing the farm loop in Loading (#1366).
			const promise = engine.startLoading(100);
			const countdown = logicalUpdateCallbacks[0];
			expect(logicalUpdateCallbacks).toHaveLength(1);

			// First tick doesn't reach zero — still ticking, not yet unhooked.
			countdown(60);
			expect(engine.loadingTime).toBe(40);
			expect(logicalUpdateCallbacks).toHaveLength(1);

			// Second tick drives it past zero — the promise resolves and the hook removes itself.
			countdown(60);
			await expect(promise).resolves.toBeUndefined();
			expect(logicalUpdateCallbacks).toHaveLength(0);
		});

		it('resolves the promise and removes the countdown hook when stopped mid-loading', async () => {
			engine.start(); // registers the engine's own logical hook
			const promise = engine.startLoading(1000);
			// The engine logical hook plus the loading countdown are both registered.
			expect(logicalUpdateCallbacks).toHaveLength(2);

			engine.stop();

			// A stop mid-cooldown must release the awaiting getNewEnemy path rather than hang it forever,
			// and tear down the countdown hook so a later start() can't resume a stale countdown.
			await expect(promise).resolves.toBeUndefined();
			expect(logicalUpdateCallbacks).toHaveLength(0);
		});

		it('resolves the promise and removes the countdown hook when reset mid-loading', async () => {
			engine.start();
			const promise = engine.startLoading(1000);
			expect(logicalUpdateCallbacks).toHaveLength(2);

			engine.reset({ id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] });

			// Reset cancels the in-flight cooldown (releasing the awaiter) but leaves the engine's own
			// logical hook in place for the re-armed battle.
			await expect(promise).resolves.toBeUndefined();
			expect(logicalUpdateCallbacks).toHaveLength(1);
		});

		it('does not leave a stale countdown hook behind when re-invoked while one is pending', async () => {
			const first = engine.startLoading(1000);
			expect(logicalUpdateCallbacks).toHaveLength(1);

			// Re-invoking before the first countdown completes cancels it (releasing its awaiter) instead
			// of stacking a second leaked hook.
			const second = engine.startLoading(500);
			await expect(first).resolves.toBeUndefined();
			expect(logicalUpdateCallbacks).toHaveLength(1);

			// The new countdown still resolves normally at zero.
			const countdown = logicalUpdateCallbacks[0];
			countdown(500);
			await expect(second).resolves.toBeUndefined();
			expect(logicalUpdateCallbacks).toHaveLength(0);
		});
	});

	describe('renderUpdate', () => {
		it('advances both battlers render cooldowns while Active', () => {
			engine.start();
			const enemyInstance = { id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);

			const playerSpy = vi.spyOn(engine.player, 'updateRenderCooldowns');
			const enemySpy = vi.spyOn(engine.enemy, 'updateRenderCooldowns');

			renderUpdateCallbacks[0](16, 16);

			expect(playerSpy).toHaveBeenCalledWith(16);
			expect(enemySpy).toHaveBeenCalledWith(16);
		});

		it('does nothing while not Active', () => {
			engine.start();
			const playerSpy = vi.spyOn(engine.player, 'updateRenderCooldowns');

			// Still Idle — no enemy loaded.
			renderUpdateCallbacks[0](16, 16);

			expect(playerSpy).not.toHaveBeenCalled();
		});
	});

	describe('logicalUpdate', () => {
		it('logs damage messages', () => {
			engine.start();
			const enemyInstance = { id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);

			logicalUpdateCallbacks[0](500);

			// A physical hit with no typed resistance carries no resist outcome (the 4th arg is undefined).
			expect(logMessage).toHaveBeenCalledWith(
				ELogType.Damage,
				expect.stringContaining('Slash'),
				'player-hit',
				undefined
			);
		});

		it('surfaces the damage type and resist outcome for a typed hit on a resistant defender (#1320)', () => {
			// A fire skill into a fire-resistant enemy: the engine names the type and flags the resist on the line.
			mockSkills[0].damagePortions = [{ type: EDamageType.Fire, weight: 1 }];
			engine.start();
			enemyLoadedCallbacks[0]({
				id: 1,
				level: 1,
				seed: 0,
				selectedSkills: [0],
				attributes: [{ attributeId: EAttribute.FireResistance, amount: 0.5 }]
			});

			logicalUpdateCallbacks[0](500);

			expect(logMessage).toHaveBeenCalledWith(
				ELogType.Damage,
				expect.stringContaining('fire damage — resisted'),
				'player-hit',
				'resisted'
			);
		});

		it('does not process updates when not Active', () => {
			engine.start();

			logicalUpdateCallbacks[0](500);

			expect(logMessage).not.toHaveBeenCalledWith(ELogType.Damage, expect.any(String));
		});

		it('accumulates timeElapsed', () => {
			engine.start();
			logicalUpdateCallbacks[0](100);
			logicalUpdateCallbacks[0](200);

			expect(engine.timeElapsed).toBe(300);
		});

		it('logs a skill-effect line when a skill applies a new effect', () => {
			mockSkills[0].effects = [
				{
					id: 1,
					target: ESkillEffectTarget.Self,
					attributeId: EAttribute.Strength,
					modifierTypeId: EModifierType.Additive,
					amount: 15,
					durationMs: 1000,
					scalingAttributeId: EAttribute.Strength,
					scalingAmount: 0
				}
			];
			engine.start();
			const enemyInstance = { id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);

			logicalUpdateCallbacks[0](500); // the 500ms-cooldown skill fires, applying its self buff

			expect(logMessage).toHaveBeenCalledWith(ELogType.SkillEffect, expect.stringContaining('empowered'));
		});

		it('aggregates damage-over-time into a single per-second summary line', () => {
			mockSkills[0].baseDamage = 0; // keep the focus on DoT — no skill kill
			engine.start();
			const enemyInstance = {
				id: 1,
				level: 1,
				seed: 0,
				selectedSkills: [0],
				attributes: [
					{ attributeId: EAttribute.Endurance, amount: 200 }, // survive the full second
					{ attributeId: EAttribute.BleedDamagePerSecond, amount: 12 }
				]
			};
			enemyLoadedCallbacks[0](enemyInstance);

			// 25 ticks of 40ms = one second; the per-tick DoT must collapse to a single log line.
			for (let i = 0; i < 25; i++) {
				logicalUpdateCallbacks[0](40);
			}

			const dotCalls = vi
				.mocked(logMessage)
				.mock.calls.filter(([type, msg]) => type === ELogType.SkillEffect && String(msg).includes('damage over time'));
			expect(dotCalls).toHaveLength(1);
			expect(dotCalls[0][1]).toBe('Goblin took 12 damage over time.');
		});

		it('aggregates heal-over-time on the player into a single per-second summary line', () => {
			mockSkills[0].baseDamage = 0; // no kill, so the full second of regen accumulates
			engine.start();
			const enemyInstance = { id: 1, level: 1, seed: 0, selectedSkills: [], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);

			engine.player.currentHealth = 10; // leave room so the regen is not capped away
			engine.player.applyEffect({
				id: 9,
				target: ESkillEffectTarget.Self,
				attributeId: EAttribute.HealthRegenPerSecond,
				modifierTypeId: EModifierType.Additive,
				amount: 8,
				durationMs: 100000,
				scalingAttributeId: EAttribute.Strength,
				scalingAmount: 0
			});

			for (let i = 0; i < 25; i++) {
				logicalUpdateCallbacks[0](40);
			}

			const hotCalls = vi
				.mocked(logMessage)
				.mock.calls.filter(([type, msg]) => type === ELogType.SkillEffect && String(msg).includes('recovered'));
			expect(hotCalls).toHaveLength(1);
			expect(hotCalls[0][1]).toBe('You recovered 8 health.');
		});

		it('summarizes player damage-over-time and enemy heal-over-time per second', () => {
			mockSkills[0].baseDamage = 0; // both battlers survive the full second
			engine.start();
			const enemyInstance = {
				id: 1,
				level: 1,
				seed: 0,
				selectedSkills: [],
				attributes: [
					{ attributeId: EAttribute.Endurance, amount: 200 },
					{ attributeId: EAttribute.HealthRegenPerSecond, amount: 5 }
				]
			};
			enemyLoadedCallbacks[0](enemyInstance);

			engine.enemy.currentHealth = 10; // leave room for the enemy's regen
			engine.player.applyEffect({
				id: 8,
				target: ESkillEffectTarget.Self,
				attributeId: EAttribute.BleedDamagePerSecond,
				modifierTypeId: EModifierType.Additive,
				amount: 6,
				durationMs: 100000,
				scalingAttributeId: EAttribute.Strength,
				scalingAmount: 0
			});

			for (let i = 0; i < 25; i++) {
				logicalUpdateCallbacks[0](40);
			}

			const effectCalls = vi.mocked(logMessage).mock.calls.filter(([type]) => type === ELogType.SkillEffect);
			expect(effectCalls).toContainEqual([ELogType.SkillEffect, 'You took 6 damage over time.']);
			expect(effectCalls).toContainEqual([ELogType.SkillEffect, 'Goblin recovered 5 health.']);
		});

		// The player-only crit/dodge outcomes (#178) and deterministic reflection (#1330) surface through the
		// combat log. Forcing a chance of 1 makes every [0,1) RNG draw succeed, so the outcome is deterministic
		// without a seed; reflection is deterministic and needs no forced roll.
		describe('crit / dodge / reflection lines', () => {
			const defaultAttributes = mockPlayerManager.attributes;
			afterEach(() => {
				mockPlayerManager.attributes = defaultAttributes;
			});

			const forcePlayerChance = (attributeId: EAttribute) => {
				mockPlayerManager.attributes = [{ attributeId, amount: 1 }];
			};
			// A tanky enemy survives the player's opening hit and fires back, giving the player something
			// to dodge/block.
			const tankyEnemy = {
				id: 1,
				level: 1,
				seed: 0,
				selectedSkills: [0],
				attributes: [{ attributeId: EAttribute.Endurance, amount: 200 }]
			};

			it('logs a critical-hit line when the player crits', () => {
				forcePlayerChance(EAttribute.CriticalChance);
				engine.start();
				enemyLoadedCallbacks[0]({ id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] });

				logicalUpdateCallbacks[0](500); // the player's 500ms skill fires and crits

				expect(logMessage).toHaveBeenCalledWith(
					ELogType.Damage,
					expect.stringContaining('You landed a critical hit with Slash'),
					'player-crit',
					undefined
				);
			});

			it('logs a dodge line when the player dodges an incoming enemy hit', () => {
				forcePlayerChance(EAttribute.DodgeChance);
				engine.start();
				enemyLoadedCallbacks[0](tankyEnemy);

				logicalUpdateCallbacks[0](500);

				expect(logMessage).toHaveBeenCalledWith(
					ELogType.Damage,
					"You dodged Goblin's Slash!",
					'player-dodge',
					undefined
				);
			});

			it('logs an enemy-reflect line when the enemy reflects the player’s hit back (#1330)', () => {
				engine.start();
				// The enemy carries 0.5 DamageReflection and enough Endurance to survive the opening hit, so a
				// positive share of the player's damage is returned to the player.
				enemyLoadedCallbacks[0]({
					id: 1,
					level: 1,
					seed: 0,
					selectedSkills: [0],
					attributes: [
						{ attributeId: EAttribute.Endurance, amount: 200 },
						{ attributeId: EAttribute.DamageReflection, amount: 0.5 }
					]
				});

				logicalUpdateCallbacks[0](500); // the player's 500ms skill fires and is partly reflected

				expect(logMessage).toHaveBeenCalledWith(
					ELogType.Damage,
					expect.stringContaining('Goblin reflected'),
					'enemy-reflect'
				);
			});

			it('logs a player-reflect line when the player reflects an incoming enemy hit back (#1330)', () => {
				// The player carries 0.5 DamageReflection (plus enough bulk to survive); a tanky enemy survives the
				// opening hit and swings back, so the player returns a share to the enemy.
				mockPlayerManager.attributes = [
					{ attributeId: EAttribute.DamageReflection, amount: 0.5 },
					{ attributeId: EAttribute.Endurance, amount: 30 },
					{ attributeId: EAttribute.Strength, amount: 50 }
				];
				engine.start();
				enemyLoadedCallbacks[0](tankyEnemy);

				logicalUpdateCallbacks[0](500);

				expect(logMessage).toHaveBeenCalledWith(
					ELogType.Damage,
					expect.stringContaining('You reflected'),
					'player-reflect'
				);
			});
		});

		// The floating-number layer consumes these events; they mirror the same flags as the combat-log
		// outcomes, spawning over whichever side was struck or defended.
		describe('combat float events', () => {
			const defaultAttributes = mockPlayerManager.attributes;
			let events: CombatFloatEvent[];
			let unhook: () => void;

			beforeEach(() => {
				events = [];
				unhook = onCombatFloat((event) => events.push(event));
			});
			afterEach(() => {
				unhook();
				mockPlayerManager.attributes = defaultAttributes;
			});

			const forcePlayerChance = (attributeId: EAttribute) => {
				mockPlayerManager.attributes = [{ attributeId, amount: 1 }];
			};
			const tankyEnemy = {
				id: 1,
				level: 1,
				seed: 0,
				selectedSkills: [0],
				attributes: [{ attributeId: EAttribute.Endurance, amount: 200 }]
			};

			it("emits an enemy-targeted hit carrying the skill's damage on a player hit", () => {
				engine.start();
				enemyLoadedCallbacks[0]({ id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] });

				logicalUpdateCallbacks[0](500);

				const hit = events.find((event) => event.target === 'enemy');
				expect(hit).toMatchObject({ target: 'enemy', kind: 'hit' });
				expect(hit?.amount).toBeGreaterThan(0);
			});

			it("carries the skill's damage type on a typed hit float (#1320)", () => {
				mockSkills[0].damagePortions = [{ type: EDamageType.Fire, weight: 1 }];
				engine.start();
				enemyLoadedCallbacks[0]({ id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] });

				logicalUpdateCallbacks[0](500);

				const hit = events.find((event) => event.target === 'enemy');
				expect(hit?.damageType).toBe(EDamageType.Fire);
			});

			it('emits an enemy-targeted crit when the player crits', () => {
				forcePlayerChance(EAttribute.CriticalChance);
				engine.start();
				enemyLoadedCallbacks[0]({ id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] });

				logicalUpdateCallbacks[0](500);

				expect(events).toContainEqual(expect.objectContaining({ target: 'enemy', kind: 'crit' }));
			});

			it('emits a player-targeted dodge with no amount when the player dodges', () => {
				forcePlayerChance(EAttribute.DodgeChance);
				engine.start();
				enemyLoadedCallbacks[0](tankyEnemy);

				logicalUpdateCallbacks[0](500);

				expect(events).toContainEqual({ target: 'player', kind: 'dodge' });
			});
		});
	});

	describe('renderUpdate', () => {
		it('interpolates skill cooldowns and active-effect countdowns while Active', () => {
			engine.start();
			const enemyInstance = { id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);

			engine.player.applyEffect({
				id: 1,
				target: ESkillEffectTarget.Self,
				attributeId: EAttribute.Strength,
				modifierTypeId: EModifierType.Additive,
				amount: 5,
				durationMs: 1000,
				scalingAttributeId: EAttribute.Strength,
				scalingAmount: 0
			});

			// The render hook is invoked with (renderDelta, logicalDelta); the engine interpolates off the
			// logical delta — 200ms into the current tick depletes the 1000ms effect's render countdown to 800.
			renderUpdateCallbacks[0](16, 200);

			expect(engine.player.activeEffects[0].renderRemainingMs).toBe(800);
			expect(engine.player.skills[0]?.renderChargeTime).toBeGreaterThan(0);
		});

		it('leaves render state untouched when not Active', () => {
			engine.start();
			const enemyInstance = { id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);
			engine.player.applyEffect({
				id: 1,
				target: ESkillEffectTarget.Self,
				attributeId: EAttribute.Strength,
				modifierTypeId: EModifierType.Additive,
				amount: 5,
				durationMs: 1000,
				scalingAttributeId: EAttribute.Strength,
				scalingAmount: 0
			});
			engine.pause();

			renderUpdateCallbacks[0](16, 200);

			expect(engine.player.activeEffects[0].renderRemainingMs).toBe(1000);
		});
	});

	describe('getOpponent', () => {
		it('returns enemy when given player', () => {
			engine.start();
			expect(engine.getOpponent(engine.player)).toBe(engine.enemy);
		});

		it('returns player when given enemy', () => {
			engine.start();
			expect(engine.getOpponent(engine.enemy)).toBe(engine.player);
		});
	});

	// An idle farm re-spawns with the player's equipment/attributes/loadout/level unchanged, so the
	// per-enemy reset re-arms the existing battler instead of rebuilding its whole attribute graph.
	describe('player reset optimization (#811)', () => {
		const defaultAttributes = mockPlayerManager.attributes;
		const defaultEquipmentStats = mockInventoryManager.equipmentStats;
		const defaultLevel = mockPlayerManager.level;
		const defaultProficiencyModifiers = mockPlayerProficiencies.battleModifiers;
		const defaultLockedBaseModifiers = mockPlayerManager.battleLockedBaseModifiers;
		const defaultSignaturePassive = mockPlayerManager.battleSignaturePassiveModifier;

		afterEach(() => {
			mockPlayerManager.attributes = defaultAttributes;
			mockInventoryManager.equipmentStats = defaultEquipmentStats;
			mockPlayerManager.level = defaultLevel;
			mockPlayerProficiencies.battleModifiers = defaultProficiencyModifiers;
			mockPlayerManager.battleLockedBaseModifiers = defaultLockedBaseModifiers;
			mockPlayerManager.battleSignaturePassiveModifier = defaultSignaturePassive;
		});

		it('re-arms the player without re-deriving when the inputs are unchanged between spawns', () => {
			engine.start(); // the first reset fully derives the player and caches its inputs
			const resetSpy = vi.spyOn(engine.player, 'reset');

			enemyLoadedCallbacks[0]({ id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] });

			// A data-less re-arm (no args) — the expensive attribute graph + skill rebuild is skipped.
			expect(resetSpy).toHaveBeenCalledTimes(1);
			expect(resetSpy).toHaveBeenCalledWith();
		});

		it('re-derives the player when the equipment stats change between spawns', () => {
			engine.start();
			const resetSpy = vi.spyOn(engine.player, 'reset');

			const newStats = [{ attributeId: EAttribute.Strength, amount: 5 }];
			mockInventoryManager.equipmentStats = newStats;
			enemyLoadedCallbacks[0]({ id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] });

			expect(resetSpy).toHaveBeenCalledWith(
				mockPlayerManager,
				newStats,
				mockInventoryManager.grantedSkillIds,
				mockPlayerProficiencies.battleModifiers,
				mockInventoryManager.equippedWeaponType
			);
		});

		it('re-derives the player when the level changes between spawns (keeps the fight card level fresh)', () => {
			engine.start();
			const resetSpy = vi.spyOn(engine.player, 'reset');

			mockPlayerManager.level = 6;
			enemyLoadedCallbacks[0]({ id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] });

			expect(resetSpy).toHaveBeenCalledWith(
				mockPlayerManager,
				mockInventoryManager.equipmentStats,
				mockInventoryManager.grantedSkillIds,
				mockPlayerProficiencies.battleModifiers,
				mockInventoryManager.equippedWeaponType
			);
		});

		// Proficiency bonuses (#982 area E / #1119) are a derivation input too: a level-up while idling
		// changes the player's proficiency modifiers, so the next spawn must rebuild the attribute graph
		// rather than re-arm the stale battler.
		it('re-derives the player when the proficiency modifiers change between spawns', () => {
			engine.start();
			const resetSpy = vi.spyOn(engine.player, 'reset');

			const newModifiers = [{ attribute: EAttribute.Strength, amount: 3, type: EModifierType.Additive, source: 4 }];
			mockPlayerProficiencies.battleModifiers = newModifiers;
			enemyLoadedCallbacks[0]({ id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] });

			expect(resetSpy).toHaveBeenCalledWith(
				mockPlayerManager,
				mockInventoryManager.equipmentStats,
				mockInventoryManager.grantedSkillIds,
				newModifiers,
				mockInventoryManager.equippedWeaponType
			);
		});

		it('re-arms (no re-derive) when the proficiency modifiers reference is unchanged', () => {
			engine.start();
			const resetSpy = vi.spyOn(engine.player, 'reset');

			// Same equipment/attributes/loadout/level and the same proficiency-modifiers reference.
			enemyLoadedCallbacks[0]({ id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] });

			expect(resetSpy).toHaveBeenCalledWith();
		});

		// The class locked base (#1126 area D) is a derivation input too: a level-up while idling rescales it,
		// so the next spawn must rebuild the attribute graph rather than re-arm the stale battler.
		it('re-derives the player when the class locked base changes between spawns', () => {
			engine.start();
			const resetSpy = vi.spyOn(engine.player, 'reset');

			const newLockedBase = [{ attribute: EAttribute.Endurance, amount: 8, type: EModifierType.Additive, source: 3 }];
			mockPlayerManager.battleLockedBaseModifiers = newLockedBase;
			enemyLoadedCallbacks[0]({ id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] });

			expect(resetSpy).toHaveBeenCalledWith(
				mockPlayerManager,
				mockInventoryManager.equipmentStats,
				mockInventoryManager.grantedSkillIds,
				newLockedBase,
				mockInventoryManager.equippedWeaponType
			);
		});

		// The class signature passive (#1126 area E) is composed at this seam — added LAST, after the rebuild's
		// setData. A full rebuild must compose it exactly once; a data-less re-arm (which skips setData) must
		// preserve it rather than dropping or double-applying it. This pins the re-arm-preservation claim that
		// the backend equivalent (BattleSnapshotTests.ToBattler_Composes...Passive) can't cover.
		it('composes the signature passive onto the player battler and preserves it across a data-less re-arm', () => {
			// A real (non-no-op) passive: +7 Strength, additive, source 9 = EAttributeModifierSource.Class.
			mockPlayerManager.battleSignaturePassiveModifier = () => ({
				attribute: EAttribute.Strength,
				amount: 7,
				type: EModifierType.Additive,
				source: 9
			});

			// The full rebuild composes the passive last: Strength = 50 (alloc) + 7 (passive), added exactly once.
			engine.start();
			expect(engine.player.attributes.getValue(EAttribute.Strength)).toBe(57);

			// A data-less re-arm (unchanged inputs) skips setData, so the already-composed passive persists — it is
			// neither lost (→ 50) nor re-applied (→ 64).
			enemyLoadedCallbacks[0]({ id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] });
			expect(engine.player.attributes.getValue(EAttribute.Strength)).toBe(57);
		});

		// The additionalModifiers passed to reset must be [...lockedBase, ...proficiency] in that order — the
		// same order the backend BattleSnapshot.GetModifiers composes them, since additive accumulation is not
		// associative in floating point. Asserted with both lists non-empty so the order is observable.
		it('composes the locked base before the proficiency modifiers at the reset seam', () => {
			engine.start();
			const resetSpy = vi.spyOn(engine.player, 'reset');

			const lockedBase = [{ attribute: EAttribute.Strength, amount: 4, type: EModifierType.Additive, source: 3 }];
			const proficiency = [{ attribute: EAttribute.Strength, amount: 2, type: EModifierType.Additive, source: 8 }];
			mockPlayerManager.battleLockedBaseModifiers = lockedBase;
			mockPlayerProficiencies.battleModifiers = proficiency;
			enemyLoadedCallbacks[0]({ id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] });

			expect(resetSpy).toHaveBeenCalledWith(
				mockPlayerManager,
				mockInventoryManager.equipmentStats,
				mockInventoryManager.grantedSkillIds,
				[...lockedBase, ...proficiency],
				mockInventoryManager.equippedWeaponType
			);
		});
	});
});
