import type { Battler } from './battler';
import { battleStep } from './battle-step';

const msPerTick = 40;
const defaultMaxMs = msPerTick * 10000;

/**
 * The deterministic outcome of a simulated battle. Mirrors the shape the backend
 * BattleResult exposes for the parity comparison (victory / playerDied / totalMs).
 */
export interface BattleResult {
	victory: boolean;
	playerDied: boolean;
	totalMs: number;
}

/**
 * Headless, deterministic battle runner — the frontend analogue of the backend's
 * `Game.Core.Battle.BattleSimulator`. It drives the shared {@link battleStep} over
 * fixed `msPerTick` ticks until one side dies or the time cap is reached, so the
 * cross-implementation parity suite exercises the exact per-tick arithmetic the
 * live {@link BattleEngine} runs rather than a hand-rolled copy.
 */
export class BattleSimulator {
	constructor(
		private readonly player: Battler,
		private readonly enemy: Battler
	) {}

	public simulate(maxMs: number = defaultMaxMs): BattleResult {
		let totalMs = msPerTick;
		for (; totalMs <= maxMs; totalMs += msPerTick) {
			battleStep(this.player, this.enemy, msPerTick);

			if (this.enemy.isDead) {
				return { victory: true, playerDied: false, totalMs };
			}
			if (this.player.isDead) {
				return { victory: false, playerDied: true, totalMs };
			}
		}

		// Mirror the backend's timeout return: the last simulated tick (maxMs), not maxMs + one tick.
		return { victory: false, playerDied: false, totalMs: totalMs - msPerTick };
	}
}
