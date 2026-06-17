<div class="board" bind:this={boardEl} bind:clientWidth={view.boardWidth}>
	<div class="gridlayer" style:background-position-x="{view.tickToX(0.5)}px"></div>

	<span class="lane-label" style="top:4px">
		<b style="color:var(--log-enemy)">enemy strikes</b> <b style="color:var(--block-color)">· your blocks</b>
	</span>
	<span class="lane-label" style="top:96px">
		<b style="color:var(--log-player)">your strikes</b> <b style="color:var(--log-enemy)">· enemy guard</b>
	</span>
	<div class="lanediv" style="top:92px"></div>

	<div class="nowzone" style:left="{view.nowX}px" style:width="{PXT}px"></div>
	<div class="nowline" style:left="{view.nowX}px"></div>

	<!-- crit columns sit behind the spans -->
	{#each game.crits as c (c.id)}
		<div class="critmark" class:used={c.used} style:transform="translateX({critX(c)}px)">✦</div>
	{/each}

	<!-- board entities -->
	{#each game.ents as e (e.id)}
		<BoardEntity entity={e} x={entX(e)} w={entW(e)} />
	{/each}

	<!-- placement preview (drag aim or hover hint) -->
	{#if preview}
		<div
			class="preview {preview.kind === 'block' ? 'block' : 'attack'}"
			class:pvhint={preview.hint}
			style:width="{preview.dur * PXT}px"
			style:transform="translateX({view.tickToX(preview.start - (preview.kind === 'block' ? 0.5 : 0))}px)"
		>
			{CARDS[preview.key].label}
		</div>
	{/if}

	<!-- resolution flashes -->
	{#each game.flashes as f (f.id)}
		<div
			class="flash"
			style:left="{view.nowX}px"
			style:top="{FLASH_LAYOUT[f.kind].y}px"
			style:color={FLASH_LAYOUT[f.kind].color}
		>
			{f.text}
		</div>
	{/each}

	{#if game.over && game.outcome}
		<OutcomeBanner outcome={game.outcome} sub={game.outcomeSub} />
	{/if}
</div>

<script lang="ts">
import { onMount } from 'svelte';
import { CARDS, PX_PER_TICK, type CritMark, type Entity, type EntityType, type FlashKind } from '$lib/card-game';
import type { CardGameView } from '../card-game-view.svelte';
import BoardEntity from './BoardEntity.svelte';
import OutcomeBanner from './OutcomeBanner.svelte';

interface Props {
	view: CardGameView;
}
const { view }: Props = $props();
const game = $derived(view.game);
const PXT = PX_PER_TICK;

/* Board placement (y px) + themeable colour for each semantic flash kind — the
   loom engine emits the kind; the presentation (pixels + CSS tokens) lives here.
   Player strikes use the player combat-log hue (--log-player); the enemy's guard
   keeps the enemy hue (--log-enemy); player blocks stay the board's block green. */
const FLASH_LAYOUT: Record<FlashKind, { y: number; color: string }> = {
	enemyHit: { y: 36, color: 'var(--enemy-accent)' },
	block: { y: 36, color: 'var(--block-color)' },
	guarded: { y: 92, color: 'var(--log-enemy)' },
	crit: { y: 116, color: 'var(--gold)' },
	strike: { y: 160, color: 'var(--log-player)' },
	channel: { y: 160, color: 'var(--rarity-epic)' },
	interrupt: { y: 160, color: 'var(--rarity-epic)' }
};

let boardEl: HTMLDivElement;

const isCover = (type: EntityType) => type === 'block' || type === 'enemyblock';

// Cover spans (blocks/guards) and crit columns sit half a tick earlier so they
// visually centre on the cell their half-open coverage rule occupies.
const entX = (e: Entity) => view.tickToX(e.start - (isCover(e.type) ? 0.5 : 0));
const entW = (e: Entity) => Math.max((e.end - e.start) * PXT, 30);
const critX = (c: CritMark) => view.tickToX(c.tick - 0.5);

// A derived local so `{#if preview}` narrows it to non-null inside the block.
const preview = $derived(view.preview);

onMount(() => {
	const onMove = (e: PointerEvent) => {
		if (!view.drag) {
			return;
		}
		view.boardLeft = boardEl.getBoundingClientRect().left;
		view.moveDrag(e.clientX, e.clientY);
	};
	const onUp = (e: PointerEvent) => {
		if (!view.drag) {
			return;
		}
		const rect = boardEl.getBoundingClientRect();
		view.boardLeft = rect.left;
		const overBoard =
			e.clientY >= rect.top - 10 && e.clientY <= rect.bottom + 50 && e.clientX >= rect.left && e.clientX <= rect.right;
		view.completeDrag(overBoard, view.tickAtClientX(e.clientX));
	};
	// A browser-cancelled pointer (e.g. an interrupting gesture) aborts the drag without casting.
	const onCancel = () => view.cancelDrag();
	window.addEventListener('pointermove', onMove);
	window.addEventListener('pointerup', onUp);
	window.addEventListener('pointercancel', onCancel);
	return () => {
		window.removeEventListener('pointermove', onMove);
		window.removeEventListener('pointerup', onUp);
		window.removeEventListener('pointercancel', onCancel);
	};
});
</script>

<style lang="scss">
.board {
	position: relative;
	height: 184px;
	overflow: hidden;
	border: 1px solid var(--border-medium);
	border-radius: 12px;
	background:
		linear-gradient(180deg, color-mix(in srgb, var(--enemy-accent) 6%, transparent), transparent 42%),
		linear-gradient(0deg, color-mix(in srgb, var(--log-enemy) 4%, transparent), transparent 42%), var(--surface);
	box-shadow: inset 0 2px 18px color-mix(in srgb, var(--black) 50%, transparent);
}

.gridlayer {
	position: absolute;
	inset: 0;
	z-index: 0;
	pointer-events: none;
	background: repeating-linear-gradient(
		90deg,
		color-mix(in srgb, var(--white) 3%, transparent) 0 1px,
		transparent 1px 24px
	);
}

.lane-label {
	position: absolute;
	left: 9px;
	z-index: 5;
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 0.1em;
	text-transform: uppercase;
	pointer-events: none;
}

.lanediv {
	position: absolute;
	left: 0;
	right: 0;
	height: 0;
	border-top: 1px dashed color-mix(in srgb, var(--white) 13%, transparent);
}

.nowzone {
	position: absolute;
	top: 0;
	bottom: 0;
	background: color-mix(in srgb, var(--accent) 5%, transparent);
	z-index: 1;
}

.nowline {
	position: absolute;
	top: 0;
	bottom: 0;
	width: 0;
	border-left: 2px solid var(--accent);
	z-index: 7;
	box-shadow: 0 0 14px 1px color-mix(in srgb, var(--accent) 55%, transparent);

	&::before {
		content: 'NOW';
		position: absolute;
		top: 3px;
		left: 6px;
		font-family: var(--mono);
		font-size: 9.5px;
		font-weight: 700;
		letter-spacing: 0.16em;
		color: var(--accent);
	}
}

/* ── crit columns ──────────────────────────────────────────────────────── */
.critmark {
	position: absolute;
	top: 92px;
	height: 92px;
	width: 24px;
	z-index: 1;
	pointer-events: none;
	will-change: transform;
	background: linear-gradient(
		180deg,
		color-mix(in srgb, var(--gold) 18%, transparent),
		color-mix(in srgb, var(--gold) 5%, transparent)
	);
	box-shadow: inset 0 0 16px color-mix(in srgb, var(--gold) 16%, transparent);
	display: flex;
	align-items: flex-end;
	justify-content: center;
	padding-bottom: 4px;
	font-family: var(--mono);
	font-weight: 700;
	color: color-mix(in srgb, var(--gold) 55%, var(--white));
	font-size: 12px;
	text-shadow: 0 0 8px color-mix(in srgb, var(--gold) 90%, transparent);

	&::before {
		content: '';
		position: absolute;
		left: 50%;
		top: 6px;
		bottom: 6px;
		width: 1px;
		transform: translateX(-50%);
		background: color-mix(in srgb, var(--gold) 55%, transparent);
		box-shadow: 0 0 8px color-mix(in srgb, var(--gold) 70%, transparent);
	}
}

.critmark.used {
	opacity: 0.14;
	box-shadow: none;
}

/* ── preview ───────────────────────────────────────────────────────────── */
.preview {
	position: absolute;
	height: 44px;
	border-radius: 7px;
	z-index: 6;
	display: flex;
	align-items: center;
	justify-content: center;
	font-family: var(--mono);
	font-size: 12px;
	color: var(--text-secondary);
	border: 1px dashed var(--border-medium);
	background: color-mix(in srgb, var(--white) 5%, transparent);
	pointer-events: none;
}

.preview.pvhint {
	opacity: 0.62;
	border-style: dotted;
}

.preview.block {
	top: 8px;
	height: 76px;
	align-items: flex-start;
	padding-top: 3px;
	border-color: color-mix(in srgb, var(--block-color) 70%, transparent);
	background: color-mix(in srgb, var(--block-color) 10%, transparent);
	color: color-mix(in srgb, var(--block-color) 35%, var(--white));
}

.preview.attack {
	top: 118px;
	border-color: color-mix(in srgb, var(--log-player) 60%, transparent);
}

/* ── flashes ───────────────────────────────────────────────────────────── */
.flash {
	position: absolute;
	z-index: 9;
	font-family: var(--mono);
	font-weight: 700;
	font-size: 21px;
	transform: translate(-50%, -50%);
	pointer-events: none;
	animation: loom-pop 0.6s ease forwards;
	text-shadow: 0 0 10px currentColor;
}

@keyframes loom-pop {
	0% {
		opacity: 0;
		transform: translate(-50%, -20%) scale(0.7);
	}
	22% {
		opacity: 1;
	}
	100% {
		opacity: 0;
		transform: translate(-50%, -120%) scale(1.1);
	}
}
</style>
