import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { EAttribute, ELogType, EModifierType, ESkillEffectTarget } from '$lib/api';
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

const { mockSkills, mockEnemies, mockAttributes, mockPlayerManager, mockInventoryManager } = vi.hoisted(() => {
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
		]
	};
	const mockInventoryManager = {
		equipmentStats: []
	};

	return { mockSkills, mockEnemies, mockAttributes, mockPlayerManager, mockInventoryManager };
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
			iconPath: ''
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

			engine.enemy.takeDamage(1e9);
			engine.resume();

			expect(engine.stage).toBe(BattleStage.Idle);
		});

		it('transitions to Defeated and logs when the player dies', () => {
			engine.start();
			const enemyInstance = { id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);

			// Kill the player, then run a sub-cooldown tick so no skill fires and the enemy survives —
			// leaving the player-dead branch as the only outcome.
			engine.player.takeDamage(1e9);
			logicalUpdateCallbacks[0](40);

			expect(engine.stage).toBe(BattleStage.Defeated);
			expect(logMessage).toHaveBeenCalledWith(ELogType.EnemyDefeated, "You've been defeated!");
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

		it('counts the loading time down each render frame and resolves + unhooks at zero', async () => {
			const promise = engine.startLoading(100);
			const countdown = renderUpdateCallbacks[0];
			expect(renderUpdateCallbacks).toHaveLength(1);

			// First frame doesn't reach zero — still ticking, not yet unhooked.
			countdown(60, 0);
			expect(engine.loadingTime).toBe(40);
			expect(renderUpdateCallbacks).toHaveLength(1);

			// Second frame drives it past zero — the promise resolves and the hook removes itself.
			countdown(60, 0);
			await expect(promise).resolves.toBeUndefined();
			expect(renderUpdateCallbacks).toHaveLength(0);
		});

		it('resolves the promise and removes the countdown hook when stopped mid-loading', async () => {
			engine.start(); // registers the engine's own render hook
			const promise = engine.startLoading(1000);
			// The engine render hook plus the loading countdown are both registered.
			expect(renderUpdateCallbacks).toHaveLength(2);

			engine.stop();

			// A stop mid-cooldown must release the awaiting getNewEnemy path rather than hang it forever,
			// and tear down every render hook so a later renderEngine.start() can't resume a stale countdown.
			await expect(promise).resolves.toBeUndefined();
			expect(renderUpdateCallbacks).toHaveLength(0);
		});

		it('resolves the promise and removes the countdown hook when reset mid-loading', async () => {
			engine.start();
			const promise = engine.startLoading(1000);
			expect(renderUpdateCallbacks).toHaveLength(2);

			engine.reset({ id: 1, level: 1, seed: 0, selectedSkills: [0], attributes: [] });

			// Reset cancels the in-flight cooldown (releasing the awaiter) but leaves the engine's own
			// render hook in place for the re-armed battle.
			await expect(promise).resolves.toBeUndefined();
			expect(renderUpdateCallbacks).toHaveLength(1);
		});

		it('does not leave a stale countdown hook behind when re-invoked while one is pending', async () => {
			const first = engine.startLoading(1000);
			expect(renderUpdateCallbacks).toHaveLength(1);

			// Re-invoking before the first countdown completes cancels it (releasing its awaiter) instead
			// of stacking a second leaked hook.
			const second = engine.startLoading(500);
			await expect(first).resolves.toBeUndefined();
			expect(renderUpdateCallbacks).toHaveLength(1);

			// The new countdown still resolves normally at zero.
			const countdown = renderUpdateCallbacks[0];
			countdown(500, 0);
			await expect(second).resolves.toBeUndefined();
			expect(renderUpdateCallbacks).toHaveLength(0);
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

			expect(logMessage).toHaveBeenCalledWith(ELogType.Damage, expect.stringContaining('Slash'), 'player-hit');
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
					durationMs: 1000
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
					{ attributeId: EAttribute.DamageTakenPerSecond, amount: 12 }
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
				durationMs: 100000
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
				attributeId: EAttribute.DamageTakenPerSecond,
				modifierTypeId: EModifierType.Additive,
				amount: 6,
				durationMs: 100000
			});

			for (let i = 0; i < 25; i++) {
				logicalUpdateCallbacks[0](40);
			}

			const effectCalls = vi.mocked(logMessage).mock.calls.filter(([type]) => type === ELogType.SkillEffect);
			expect(effectCalls).toContainEqual([ELogType.SkillEffect, 'You took 6 damage over time.']);
			expect(effectCalls).toContainEqual([ELogType.SkillEffect, 'Goblin recovered 5 health.']);
		});

		// The player-only crit/dodge/block outcomes (#178) surface through the combat log. Forcing a
		// chance of 1 makes every [0,1) RNG draw succeed, so the outcome is deterministic without a seed.
		describe('crit / dodge / block lines (#178)', () => {
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
					'player-crit'
				);
			});

			it('logs a dodge line when the player dodges an incoming enemy hit', () => {
				forcePlayerChance(EAttribute.DodgeChance);
				engine.start();
				enemyLoadedCallbacks[0](tankyEnemy);

				logicalUpdateCallbacks[0](500);

				expect(logMessage).toHaveBeenCalledWith(ELogType.Damage, "You dodged Goblin's Slash!", 'player-dodge');
			});

			it('logs a block line when the player blocks an incoming enemy hit', () => {
				forcePlayerChance(EAttribute.BlockChance);
				engine.start();
				enemyLoadedCallbacks[0](tankyEnemy);

				logicalUpdateCallbacks[0](500);

				expect(logMessage).toHaveBeenCalledWith(
					ELogType.Damage,
					expect.stringContaining("You blocked Goblin's Slash"),
					'player-block'
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

			it('emits a player-targeted block when the player blocks', () => {
				forcePlayerChance(EAttribute.BlockChance);
				engine.start();
				enemyLoadedCallbacks[0](tankyEnemy);

				logicalUpdateCallbacks[0](500);

				expect(events).toContainEqual(expect.objectContaining({ target: 'player', kind: 'block' }));
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
				durationMs: 1000
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
				durationMs: 1000
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
});
