/* Card Game screen view-model.

   Owns the (statified, reactive) Loom engine plus the screen's interaction
   state — board geometry, the in-flight drag, and the hover hint — and derives
   the placement preview from them. Pointer→tick conversion that needs the live
   DOM rect is fed in by the Board component (it holds the element); everything
   else lives here so the components stay thin. */

import { statify } from '$lib/common/statify.svelte';
import { LoomGame, NOW_FRACTION, PX_PER_TICK, type CardKind } from '$lib/card-game';

interface DragState {
	index: number;
	key: string;
	startX: number;
	startY: number;
	/** Latest pointer x (client coords) while dragging. */
	pointerX: number;
	/** Set once the pointer has moved past the drag threshold. */
	started: boolean;
}

export interface PlacementPreview {
	key: string;
	kind: CardKind;
	start: number;
	dur: number;
	/** True for the passive hover hint (vs. an active drag). */
	hint: boolean;
}

const DRAG_THRESHOLD_PX = 6;

export class CardGameView {
	/** The reactive simulation. Statify makes its fields drive the UI. */
	readonly game = statify(new LoomGame());

	/** Live board geometry, written by the Board component. */
	boardWidth = $state(0);
	boardLeft = $state(0);

	/** The card currently hovered in the hand (drives the placement hint). */
	hoverKey = $state<string | null>(null);
	/** The in-flight drag, or null. */
	drag = $state<DragState | null>(null);

	/** NOW line x-position within the board (px). */
	get nowX(): number {
		return this.boardWidth * NOW_FRACTION;
	}

	/** Where a card would land if cast right now — an active drag aim, else the
	 *  hover hint at the lane tail. Null when nothing is hovered/dragged. */
	get preview(): PlacementPreview | null {
		if (this.game.over) {
			return null;
		}
		const d = this.drag;
		if (d?.started) {
			const desired = Math.round((d.pointerX - this.boardLeft - this.nowX) / PX_PER_TICK + this.game.playTick);
			const p = this.game.aimPlacement(d.key, desired);
			return { key: d.key, kind: p.kind, start: p.start, dur: p.dur, hint: false };
		}
		if (this.hoverKey) {
			const p = this.game.tailPlacement(this.hoverKey);
			return { key: this.hoverKey, kind: p.kind, start: p.start, dur: p.dur, hint: true };
		}
		return null;
	}

	/* ── drag lifecycle ────────────────────────────────────────────────── */
	beginDrag(index: number, key: string, clientX: number, clientY: number): void {
		if (this.game.over) {
			return;
		}
		this.drag = { index, key, startX: clientX, startY: clientY, pointerX: clientX, started: false };
	}

	moveDrag(clientX: number, clientY: number): void {
		const d = this.drag;
		if (!d) {
			return;
		}
		if (!d.started && Math.hypot(clientX - d.startX, clientY - d.startY) > DRAG_THRESHOLD_PX) {
			d.started = true;
		}
		d.pointerX = clientX;
	}

	/** Finish a drag. `overBoard`/`desiredStart` come from the Board (it owns the
	 *  rect): a click (no movement) quick-casts to the tail; a release over the
	 *  board aims to `desiredStart`; anywhere else cancels. */
	completeDrag(overBoard: boolean, desiredStart: number): void {
		const d = this.drag;
		this.drag = null;
		if (!d || this.game.over) {
			return;
		}
		if (!d.started) {
			this.game.castSlot(d.index);
		} else if (overBoard) {
			this.game.castSlot(d.index, desiredStart);
		}
		this.hoverKey = null;
	}

	/** Convert a client x-coordinate to a board tick (for the aimed cast). */
	tickAtClientX(clientX: number): number {
		return Math.round((clientX - this.boardLeft - this.nowX) / PX_PER_TICK + this.game.playTick);
	}

	/* ── hover hint ────────────────────────────────────────────────────── */
	setHover(key: string): void {
		if (!this.game.over) {
			this.hoverKey = key;
		}
	}
	clearHover(key: string): void {
		if (this.hoverKey === key) {
			this.hoverKey = null;
		}
	}

	/* ── input passthroughs ────────────────────────────────────────────── */
	castSlot(index: number): void {
		this.game.castSlot(index);
	}
	setReflex(on: boolean): void {
		this.game.setReflex(on);
	}
	setStat(stat: 'agi' | 'dex' | 'luck', value: number): void {
		this.game.setStat(stat, value);
	}
	reset(): void {
		this.game.reset();
	}
}
