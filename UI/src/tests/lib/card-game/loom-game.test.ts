import { describe, it, expect } from 'vitest';
import { LoomGame } from '$lib/card-game';

/* A constant rng makes scheduling deterministic:
   - enemy strikes:  dmg 8, every 7 ticks from tick 7  → resolve at 9, 16, 23, …
   - enemy guards:   span 4, from tick 16              → [16,20), [31,35), …
   - enemy channel:  windup 8, dmg 28, from tick 18    → resolve at 26
   - crit marks (LUCK 14 → gap 12): ticks 11, 23, …                              */
const zeroRng = () => 0;

/** Step the sim until it passes `targetTick` (or a safety cap is hit). */
function advanceTo(game: LoomGame, targetTick: number, step = 0.05, maxSteps = 4000): void {
	let n = 0;
	while (game.playTick < targetTick && n++ < maxSteps) {
		game.advance(step);
	}
}

describe('LoomGame — initial state', () => {
	it('starts at full HP with a four-card hand drawn from the deck', () => {
		const game = new LoomGame(zeroRng);
		expect(game.enemyHP).toBe(120);
		expect(game.playerHP).toBe(80);
		expect(game.hand.length).toBe(4);
		expect(game.deck.length).toBe(10); // 14-card starter minus the opening hand
		expect(game.discard.length).toBe(0);
		expect(game.over).toBe(false);
	});

	it('exposes attribute-derived getters', () => {
		const game = new LoomGame(zeroRng);
		game.dex = 50;
		game.agi = 200;
		game.luck = 0;
		expect(game.handCap).toBe(5);
		expect(game.drawIntervalSec).toBeCloseTo(1.0);
		expect(game.critGap).toBe(26);
	});
});

describe('LoomGame — casting', () => {
	it('moves a cast card from hand to discard and onto the board', () => {
		const game = new LoomGame(zeroRng);
		game.hand = [{ id: 1, key: 'slash' }];
		game.castSlot(0);
		expect(game.hand.length).toBe(0);
		expect(game.discard).toEqual(['slash']);
		expect(game.ents.filter((e) => e.type === 'attack').length).toBe(1);
	});

	it('chains two same-type strikes tail-to-tail (no overlap)', () => {
		const game = new LoomGame(zeroRng);
		game.hand = [
			{ id: 1, key: 'slash' },
			{ id: 2, key: 'slash' }
		];
		game.castSlot(0); // quick-cast → [0,3]
		game.castSlot(0); // quick-cast → [3,6]
		const strikes = game.ents.filter((e) => e.type === 'attack').sort((a, b) => a.start - b.start);
		expect(strikes.map((s) => [s.start, s.end])).toEqual([
			[0, 3],
			[3, 6]
		]);
	});

	it('aims a card up its lane to a chosen start', () => {
		const game = new LoomGame(zeroRng);
		game.hand = [{ id: 1, key: 'guard' }];
		game.castSlot(0, 12); // aimed at tick 12, span 7
		const block = game.ents.find((e) => e.type === 'block');
		expect(block).toBeDefined();
		expect([block?.start, block?.end]).toEqual([12, 19]);
	});

	it('ignores casts once the duel is over', () => {
		const game = new LoomGame(zeroRng);
		game.hand = [{ id: 1, key: 'slash' }];
		game.over = true;
		game.castSlot(0);
		expect(game.hand.length).toBe(1);
		expect(game.ents.length).toBe(0);
	});
});

describe('LoomGame — resolution', () => {
	it('a strike resolving inside the boss guard deals 40% (16 → 6)', () => {
		const game = new LoomGame(zeroRng);
		game.hand = [{ id: 1, key: 'slash' }];
		game.castSlot(0, 14); // [14,17], resolves at 17, inside enemy guard [16,20)
		advanceTo(game, 18);
		expect(game.enemyHP).toBe(114); // 120 - ⌊16·0.4⌋
	});

	it('a strike resolving on a crit tick deals double (16 → 32)', () => {
		const game = new LoomGame(zeroRng);
		game.hand = [{ id: 1, key: 'slash' }];
		game.castSlot(0, 8); // [8,11], resolves at 11, on the crit mark at tick 11
		advanceTo(game, 12);
		expect(game.enemyHP).toBe(88); // 120 - (16·2)
	});

	it('a maintained block wall stops incoming enemy strikes', () => {
		const game = new LoomGame(zeroRng);
		// cover ticks 9 and 16 (the first two enemy resolves) with guards
		game.hand = [
			{ id: 1, key: 'guard' },
			{ id: 2, key: 'guard' }
		];
		game.castSlot(0, 7); // [7,14) covers resolve 9; hand re-indexes after the splice
		game.castSlot(0, 14); // [14,21) covers resolve 16
		advanceTo(game, 18);
		expect(game.playerHP).toBe(80); // both hits blocked
	});

	it('an unblocked enemy strike chips the player', () => {
		const game = new LoomGame(zeroRng);
		advanceTo(game, 10); // first enemy strike (dmg 8) resolves at tick 9
		expect(game.playerHP).toBe(72);
	});
});

describe('LoomGame — resolution flashes', () => {
	/* The engine emits presentation-free semantic flash kinds; the Board maps
	   each to a board position + themeable colour. These pin the kind each
	   resolution path emits. */
	const kinds = (game: LoomGame) => game.flashes.map((f) => f.kind);

	it('emits an "enemyHit" flash when an enemy strike lands unblocked', () => {
		const game = new LoomGame(zeroRng);
		advanceTo(game, 10); // first enemy strike resolves unblocked at tick 9
		expect(kinds(game)).toContain('enemyHit');
	});

	it('emits a "block" flash (and no others on that hit) when a strike is blocked', () => {
		const game = new LoomGame(zeroRng);
		game.hand = [{ id: 1, key: 'guard' }];
		game.castSlot(0, 7); // [7,14) covers the enemy resolve at 9
		advanceTo(game, 10);
		expect(kinds(game)).toContain('block');
		expect(kinds(game)).not.toContain('enemyHit');
	});

	it('emits "crit" and "strike" flashes when a strike doubles on a crit tick', () => {
		const game = new LoomGame(zeroRng);
		game.hand = [{ id: 1, key: 'slash' }];
		game.castSlot(0, 8); // [8,11], resolves on the crit mark at tick 11
		advanceTo(game, 12);
		expect(kinds(game)).toEqual(expect.arrayContaining(['crit', 'strike']));
	});

	it('emits a "guarded" flash when a strike resolves inside the boss guard', () => {
		const game = new LoomGame(zeroRng);
		game.hand = [{ id: 1, key: 'slash' }];
		game.castSlot(0, 14); // [14,17], resolves at 17 inside enemy guard [16,20)
		advanceTo(game, 18);
		expect(kinds(game)).toEqual(expect.arrayContaining(['guarded', 'strike']));
	});

	it('emits a "channel" flash when a player Channel resolves', () => {
		const game = new LoomGame(zeroRng);
		game.hand = [{ id: 1, key: 'channel' }];
		game.castSlot(0); // quick-cast [0,9]; resolves at 9 before any enemy hit can interrupt it
		advanceTo(game, 10);
		expect(kinds(game)).toContain('channel');
		expect(game.enemyHP).toBe(74); // 120 - 46
	});

	it('emits an "interrupt" flash and cancels the Channel when an unblocked hit lands mid-wind-up', () => {
		const game = new LoomGame(zeroRng);
		game.hand = [{ id: 1, key: 'channel' }];
		game.castSlot(0, 3); // [3,12], still winding up when the enemy strike resolves at 9
		advanceTo(game, 10);
		expect(kinds(game)).toContain('interrupt');
		const channel = game.ents.find((e) => e.type === 'channel');
		expect(channel?.cancelled).toBe(true);
	});
});

describe('LoomGame — deck economy', () => {
	it('draws over time to refill the hand toward the cap', () => {
		const game = new LoomGame(zeroRng); // hand 4, deck 10, drawInterval 1.95s
		game.hand.splice(0, 1); // simulate a cast → hand 3
		expect(game.hand.length).toBe(3);
		// advance ~2s of real time; one draw should fire
		for (let i = 0; i < 45 && game.hand.length < 4; i++) {
			game.advance(0.05);
		}
		expect(game.hand.length).toBe(4);
		expect(game.deck.length).toBe(9);
	});
});

describe('LoomGame — outcome & reflex', () => {
	it('declares victory when the enemy is downed', () => {
		const game = new LoomGame(zeroRng);
		game.enemyHP = 0;
		game.advance(0.02);
		expect(game.over).toBe(true);
		expect(game.outcome).toBe('win');
	});

	it('declares defeat when the player is downed', () => {
		const game = new LoomGame(zeroRng);
		game.playerHP = 0;
		game.advance(0.02);
		expect(game.over).toBe(true);
		expect(game.outcome).toBe('lose');
	});

	it('reflex slow-time engages only while reserve remains and drains it', () => {
		const game = new LoomGame(zeroRng);
		game.setReflex(true);
		expect(game.slow).toBe(true);
		game.advance(0.05);
		expect(game.reflex).toBeLessThan(100);

		game.reflex = 0;
		game.setReflex(true);
		expect(game.slow).toBe(false); // no reserve → cannot slow
	});

	it('resets back to a clean opening state', () => {
		const game = new LoomGame(zeroRng);
		advanceTo(game, 20);
		game.reset();
		expect(game.playTick).toBe(0);
		expect(game.enemyHP).toBe(120);
		expect(game.playerHP).toBe(80);
		expect(game.hand.length).toBe(4);
		expect(game.ents.length).toBe(0);
		expect(game.over).toBe(false);
	});
});
