/* Card-Boss Duel — "The Initiative Loom" engine.

   A self-contained, real-time simulation of the duel: the present (NOW) is a
   moving deadline, cards are multi-tick spans laid onto a scrolling timeline,
   and entities resolve the instant their right edge crosses NOW. This is a
   plain class (no Svelte runes, no DOM) so it stays fully unit-testable; the
   screen wraps it with `statify` to make its fields reactive, and drives
   `advance(dt)` from a requestAnimationFrame loop.

   Randomness (enemy cadence, crit placement, deck shuffle, damage rolls) is
   injected via `rng` so tests are deterministic and it can later be seeded.

   This is a demo: HP, the deck, and enemy cadences are hardcoded sample
   values. When wired to real data they become character/encounter inputs and
   the admin-configurable tunables in `loom-core.ts`. */

import { CARDS, STARTER_DECK, cardDuration, type CardKind } from './cards';
import {
	BASE_RATE,
	CULL_TICKS,
	REFLEX_SCALE,
	SCHEDULE_HORIZON,
	critGap,
	drawInterval,
	freeStart,
	handCap,
	isAttackKind,
	resolveStrike,
	shuffle,
	spanActiveAt,
	type Span
} from './loom-core';

export type EntityType = 'enemyhit' | 'enemyblock' | 'enemychannel' | 'attack' | 'channel' | 'block';

export interface Entity {
	id: number;
	type: EntityType;
	/** Inclusive start tick of the span. */
	start: number;
	/** Exclusive end tick (the resolve point for attacks). */
	end: number;
	/** Tick at which a damaging entity resolves (== end); undefined for blocks/guards. */
	resolve?: number;
	/** Damage dealt on resolve (damaging entities only). */
	dmg?: number;
	resolved: boolean;
	/** A player Channel cancelled by an unblocked enemy hit mid-wind-up. */
	cancelled?: boolean;
	/** Display text for the card/span face. */
	label: string;
}

export interface CritMark {
	id: number;
	tick: number;
	used: boolean;
}

/** Semantic kind of a resolution flash. The Board maps each to a board
 *  position and themeable colour, so the engine stays free of pixels and CSS. */
export type FlashKind = 'block' | 'enemyHit' | 'interrupt' | 'crit' | 'guarded' | 'strike' | 'channel';

export interface FlashFx {
	id: number;
	/** What resolved; the Board resolves this to a board y-position and colour. */
	kind: FlashKind;
	/** Display text for the flash (a damage number or an outcome label). */
	text: string;
	/** Remaining lifetime in seconds. */
	ttl: number;
}

/** A card occupying a hand slot. Each gets a stable id so the UI can key the
 *  hand by instance (drawing adds one node without rebuilding the others). */
export interface HandCard {
	id: number;
	key: string;
}

export type Outcome = 'win' | 'lose' | null;

export class LoomGame {
	/* ── timeline ──────────────────────────────────────────────────────── */
	playTick = 0;
	prevTick = 0;

	/* ── combatants ────────────────────────────────────────────────────── */
	enemyHP = 120;
	enemyMax = 120;
	playerHP = 80;
	playerMax = 80;

	/* ── reflex slow-time ──────────────────────────────────────────────── */
	/** Agility "reserve", 0–100; drains while held, regenerates while idle. */
	reflex = 100;
	slow = false;

	/* ── board ─────────────────────────────────────────────────────────── */
	ents: Entity[] = [];
	crits: CritMark[] = [];
	flashes: FlashFx[] = [];

	/* ── deck economy ──────────────────────────────────────────────────── */
	deck: string[] = [];
	hand: HandCard[] = [];
	discard: string[] = [];
	/** Seconds accumulated toward the next draw. */
	drawAcc = 0;

	/* ── sandbox attributes (replaced by real character data later) ─────── */
	agi = 10;
	dex = 8;
	luck = 14;

	/* ── outcome ───────────────────────────────────────────────────────── */
	over = false;
	outcome: Outcome = null;
	outcomeSub = '';

	/* ── scheduling cursors ────────────────────────────────────────────── */
	private nextEnemyTick = 7;
	private nextEnemyBlockTick = 16;
	private nextEnemyChannelTick = 18;
	private nextCritTick = 11;
	private blockTail = 0;
	private atkTail = 0;

	private nextEntId = 1;
	private nextHandId = 1;
	private nextFlashId = 1;

	private readonly rng: () => number;

	constructor(rng: () => number = Math.random) {
		this.rng = rng;
		this.reset();
	}

	/* ── derived attribute formulas ────────────────────────────────────── */
	get handCap(): number {
		return handCap(this.dex);
	}
	get critGap(): number {
		return critGap(this.luck);
	}
	get drawIntervalSec(): number {
		return drawInterval(this.agi);
	}

	/* ── lane views ────────────────────────────────────────────────────── */
	private get blockLane(): Span[] {
		return this.ents.filter((e) => e.type === 'block');
	}
	private get attackLane(): Span[] {
		return this.ents.filter((e) => e.type === 'attack' || e.type === 'channel');
	}
	private get enemyGuardLane(): Span[] {
		return this.ents.filter((e) => e.type === 'enemyblock');
	}

	/* ── lifecycle ─────────────────────────────────────────────────────── */
	reset(): void {
		this.playTick = 0;
		this.prevTick = 0;
		this.enemyHP = this.enemyMax;
		this.playerHP = this.playerMax;
		this.reflex = 100;
		this.slow = false;
		this.ents = [];
		this.crits = [];
		this.flashes = [];
		this.deck = shuffle([...STARTER_DECK], this.rng);
		this.hand = [];
		this.discard = [];
		this.drawAcc = 0;
		this.over = false;
		this.outcome = null;
		this.outcomeSub = '';
		this.nextEnemyTick = 7;
		this.nextEnemyBlockTick = 16;
		this.nextEnemyChannelTick = 18;
		this.nextCritTick = 11;
		this.blockTail = 0;
		this.atkTail = 0;
		for (let i = 0; i < 4 && this.deck.length; i++) {
			this.hand.push({ id: this.nextHandId++, key: this.deck.pop() as string });
		}
	}

	/** Advance the simulation by `dt` seconds (clamped to avoid large jumps). */
	advance(dt: number): void {
		if (this.over) {
			return;
		}
		if (dt > 0.05) {
			dt = 0.05;
		}

		// Reflex reserve drains while held (slower with Agility), else regenerates.
		if (this.slow && this.reflex > 0) {
			this.reflex = Math.max(0, this.reflex - Math.max(8, 28 - this.agi) * dt);
			if (this.reflex === 0) {
				this.slow = false;
			}
		} else if (!this.slow && this.reflex < 100) {
			this.reflex = Math.min(100, this.reflex + 11 * dt);
		}

		let rate = BASE_RATE;
		if (this.slow && this.reflex > 0) {
			rate *= REFLEX_SCALE;
		}
		this.prevTick = this.playTick;
		this.playTick += rate * dt;

		this.drawAcc += dt;
		if (this.drawAcc >= this.drawIntervalSec) {
			this.drawAcc = 0;
			this.drawOne();
		}

		this.scheduleEnemy();
		this.scheduleEnemyChannel();
		this.scheduleEnemyBlock();
		this.scheduleCrit();
		this.resolveCrossings();
		this.cull();
		this.decayFlashes(dt);
		this.checkOutcome();
	}

	/* ── scheduling ────────────────────────────────────────────────────── */
	private scheduleEnemy(): void {
		while (this.nextEnemyTick < this.playTick + SCHEDULE_HORIZON) {
			const t = this.nextEnemyTick;
			const dmg = 8 + Math.floor(this.rng() * 9);
			this.ents.push({
				id: this.nextEntId++,
				type: 'enemyhit',
				start: t,
				end: t + 2,
				resolve: t + 2,
				dmg,
				resolved: false,
				label: `⚔ ${dmg}`
			});
			this.nextEnemyTick += 7 + Math.floor(this.rng() * 7);
		}
	}

	private scheduleEnemyBlock(): void {
		while (this.nextEnemyBlockTick < this.playTick + SCHEDULE_HORIZON) {
			const t = this.nextEnemyBlockTick;
			const span = 4 + Math.floor(this.rng() * 3);
			this.ents.push({
				id: this.nextEntId++,
				type: 'enemyblock',
				start: t,
				end: t + span,
				resolved: false,
				label: 'GUARD'
			});
			this.nextEnemyBlockTick += 15 + Math.floor(this.rng() * 12);
		}
	}

	private scheduleEnemyChannel(): void {
		while (this.nextEnemyChannelTick < this.playTick + SCHEDULE_HORIZON) {
			const t = this.nextEnemyChannelTick;
			const windup = 8 + Math.floor(this.rng() * 3);
			const dmg = 28 + Math.floor(this.rng() * 12);
			this.ents.push({
				id: this.nextEntId++,
				type: 'enemychannel',
				start: t,
				end: t + windup,
				resolve: t + windup,
				dmg,
				resolved: false,
				label: `CHANNEL · ${dmg}`
			});
			this.nextEnemyChannelTick += 26 + Math.floor(this.rng() * 16);
		}
	}

	private scheduleCrit(): void {
		while (this.nextCritTick < this.playTick + SCHEDULE_HORIZON) {
			this.crits.push({ id: this.nextEntId++, tick: this.nextCritTick, used: false });
			this.nextCritTick += this.critGap;
		}
	}

	/* ── casting ───────────────────────────────────────────────────────── */

	/** Placement (start tick + duration) a quick-cast of `key` would take — the
	 *  tail of its lane. Used for the hover hint. */
	tailPlacement(key: string): { start: number; dur: number; kind: CardKind } {
		const card = CARDS[key];
		const dur = cardDuration(card);
		const isAtk = isAttackKind(card.kind);
		const desired = Math.max(Math.ceil(this.playTick), isAtk ? this.atkTail : this.blockTail);
		const lane = isAtk ? this.attackLane : this.blockLane;
		return { start: freeStart(lane, desired, dur, this.playTick), dur, kind: card.kind };
	}

	/** Placement a card would take if aimed at `desiredStart` (snapped to the
	 *  nearest free slot). Used for the drag preview. */
	aimPlacement(key: string, desiredStart: number): { start: number; dur: number; kind: CardKind } {
		const card = CARDS[key];
		const dur = cardDuration(card);
		const lane = isAttackKind(card.kind) ? this.attackLane : this.blockLane;
		return { start: freeStart(lane, desiredStart, dur, this.playTick), dur, kind: card.kind };
	}

	/** Cast the hand card at `index`; `desiredStart` aims it (omit to quick-cast
	 *  to the lane tail). Moves the card to the discard pile. */
	castSlot(index: number, desiredStart?: number): void {
		if (this.over || index < 0 || index >= this.hand.length) {
			return;
		}
		const card = this.hand[index];
		this.place(card.key, desiredStart);
		this.discard.push(card.key);
		this.hand.splice(index, 1);
	}

	private place(key: string, desiredStart?: number): void {
		const card = CARDS[key];
		const dur = cardDuration(card);
		const isAtk = isAttackKind(card.kind);
		const desired =
			desiredStart != null ? desiredStart : Math.max(Math.ceil(this.playTick), isAtk ? this.atkTail : this.blockTail);
		const lane = isAtk ? this.attackLane : this.blockLane;
		const start = freeStart(lane, desired, dur, this.playTick);
		const end = start + dur;
		if (card.kind === 'block') {
			this.ents.push({ id: this.nextEntId++, type: 'block', start, end, resolved: false, label: card.label });
			this.blockTail = Math.max(this.blockTail, end);
		} else {
			this.ents.push({
				id: this.nextEntId++,
				type: card.kind,
				start,
				end,
				resolve: end,
				dmg: card.dmg,
				resolved: false,
				label: card.label
			});
			this.atkTail = Math.max(this.atkTail, end);
		}
	}

	/* ── deck draw ─────────────────────────────────────────────────────── */
	private drawOne(): boolean {
		if (this.hand.length >= this.handCap) {
			return false;
		}
		if (!this.deck.length) {
			if (this.discard.length) {
				this.deck = shuffle(this.discard, this.rng);
				this.discard = [];
			} else {
				return false;
			}
		}
		this.hand.push({ id: this.nextHandId++, key: this.deck.pop() as string });
		return true;
	}

	/* ── resolution ────────────────────────────────────────────────────── */
	private resolveCrossings(): void {
		const a = this.prevTick;
		const b = this.playTick;
		for (const e of this.ents) {
			if (e.resolved || e.resolve == null) {
				continue;
			}
			// `resolve` is narrowed to a number by the null check above; pass it through so the
			// handlers don't need to re-assert it.
			const resolve = e.resolve;
			if (resolve > a && resolve <= b) {
				if (e.type === 'enemyhit' || e.type === 'enemychannel') {
					this.resolveEnemyHit(e, resolve);
				} else {
					this.resolvePlayerStrike(e, resolve);
				}
			}
		}
	}

	private resolveEnemyHit(e: Entity, resolve: number): void {
		e.resolved = true;
		if (spanActiveAt(this.blockLane, resolve)) {
			this.flash('block', 'BLOCK');
			return;
		}
		this.playerHP = Math.max(0, this.playerHP - (e.dmg ?? 0));
		this.flash('enemyHit', `-${e.dmg}`);
		// An unblocked hit interrupts a player Channel mid-wind-up.
		for (const ch of this.ents) {
			if (ch.type === 'channel' && !ch.resolved && resolve >= ch.start && resolve < ch.end) {
				ch.resolved = true;
				ch.cancelled = true;
				this.flash('interrupt', 'interrupted');
			}
		}
	}

	private resolvePlayerStrike(e: Entity, resolve: number): void {
		e.resolved = true;
		const outcome = resolveStrike(e.dmg ?? 0, resolve, this.crits, this.enemyGuardLane);
		if (outcome.crit) {
			const mark = this.crits.find((c) => !c.used && Math.round(c.tick) === Math.round(resolve));
			if (mark) {
				mark.used = true;
			}
			this.flash('crit', 'CRIT ×2');
		}
		if (outcome.guarded) {
			this.flash('guarded', 'GUARDED');
		}
		this.enemyHP = Math.max(0, this.enemyHP - outcome.dmg);
		this.flash(e.type === 'channel' ? 'channel' : 'strike', `-${outcome.dmg}`);
	}

	/* ── reflex ────────────────────────────────────────────────────────── */
	setReflex(on: boolean): void {
		this.slow = on && this.reflex > 0;
	}

	/* ── sandbox ───────────────────────────────────────────────────────── */
	setStat(stat: 'agi' | 'dex' | 'luck', value: number): void {
		this[stat] = value;
	}

	/* ── housekeeping ──────────────────────────────────────────────────── */
	private flash(kind: FlashKind, text: string): void {
		this.flashes.push({ id: this.nextFlashId++, kind, text, ttl: 0.6 });
	}

	private decayFlashes(dt: number): void {
		if (!this.flashes.length) {
			return;
		}
		for (const f of this.flashes) {
			f.ttl -= dt;
		}
		this.flashes = this.flashes.filter((f) => f.ttl > 0);
	}

	private cull(): void {
		const cutoff = this.playTick - CULL_TICKS;
		this.ents = this.ents.filter((e) => e.end >= cutoff);
		this.crits = this.crits.filter((c) => c.tick >= cutoff);
	}

	private checkOutcome(): void {
		if (this.over) {
			return;
		}
		if (this.enemyHP <= 0) {
			this.over = true;
			this.outcome = 'win';
			this.outcomeSub = 'The Warden unravels. Loot drops.';
		} else if (this.playerHP <= 0) {
			this.over = true;
			this.outcome = 'lose';
			this.outcomeSub = 'The present caught you.';
		}
	}
}
