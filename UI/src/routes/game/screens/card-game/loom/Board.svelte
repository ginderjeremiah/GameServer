<div class="board" bind:this={boardEl} bind:clientWidth={view.boardWidth}>
	<div class="gridlayer" style:background-position-x="{view.nowX + (0.5 - game.playTick) * PXT}px"></div>

	<span class="lane-label" style="top:4px">
		<b style="color:var(--log-enemy)">enemy strikes</b> <b style="color:var(--health-remaining-color)">· your blocks</b>
	</span>
	<span class="lane-label" style="top:96px">
		<b style="color:var(--log-enemy)">your strikes</b> <b style="color:var(--log-enemy)">· enemy guard</b>
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
		<div
			class="ent {e.type}"
			class:resolved={e.resolved}
			class:cancelled={e.cancelled}
			style:width="{entW(e)}px"
			style:transform="translateX({entX(e)}px)"
		>
			{#if hasWindup(e.type)}<div class="windup"></div>{/if}
			{#if e.type === 'enemychannel'}<span class="ecl">{e.label}</span>{:else}{e.label}{/if}
			{#if hasTip(e.type)}<span class="tip"></span>{/if}
		</div>
	{/each}

	<!-- placement preview (drag aim or hover hint) -->
	{#if preview}
		<div
			class="preview {preview.kind === 'block' ? 'block' : 'attack'}"
			class:pvhint={preview.hint}
			style:width="{preview.dur * PXT}px"
			style:transform="translateX({view.nowX +
				(preview.start - game.playTick) * PXT -
				(preview.kind === 'block' ? PXT / 2 : 0)}px)"
		>
			{CARDS[preview.key].label}
		</div>
	{/if}

	<!-- resolution flashes -->
	{#each game.flashes as f (f.id)}
		<div class="flash" style:left="{view.nowX}px" style:top="{f.y}px" style:color={f.color}>{f.text}</div>
	{/each}

	{#if game.over}
		<div class="banner {game.outcome}">
			<h2>{game.outcome === 'win' ? 'VICTORY' : 'DOWNED'}</h2>
			<span class="bsub">{game.outcomeSub}</span>
		</div>
	{/if}
</div>

<script lang="ts">
import { onMount } from 'svelte';
import { CARDS, PX_PER_TICK, type CritMark, type Entity, type EntityType } from '$lib/card-game';
import type { CardGameView } from '../card-game-view.svelte';

interface Props {
	view: CardGameView;
}
const { view }: Props = $props();
const game = $derived(view.game);
const PXT = PX_PER_TICK;

let boardEl: HTMLDivElement;

const isCover = (type: EntityType) => type === 'block' || type === 'enemyblock';
const hasWindup = (type: EntityType) => type === 'attack' || type === 'enemychannel';
const hasTip = (type: EntityType) =>
	type === 'enemyhit' || type === 'enemychannel' || type === 'attack' || type === 'channel';

const entX = (e: Entity) => view.nowX + (e.start - game.playTick) * PXT - (isCover(e.type) ? PXT / 2 : 0);
const entW = (e: Entity) => Math.max((e.end - e.start) * PXT, 30);
const critX = (c: CritMark) => view.nowX + (c.tick - game.playTick) * PXT - PXT / 2;

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

/* ── entities ──────────────────────────────────────────────────────────── */
.ent {
	position: absolute;
	height: 44px;
	border-radius: 7px;
	display: flex;
	align-items: center;
	justify-content: center;
	gap: 5px;
	font-family: var(--mono);
	font-weight: 500;
	font-size: 12px;
	will-change: transform;
	overflow: hidden;
	z-index: 3;
	border: 1px solid;
	color: var(--text-primary);
}

.ent .tip {
	position: absolute;
	right: -1px;
	top: -1px;
	bottom: -1px;
	width: 3px;
	z-index: 4;
}

.ent .windup {
	position: absolute;
	left: 0;
	top: 0;
	bottom: 0;
	right: 4px;
	background: repeating-linear-gradient(
		45deg,
		color-mix(in srgb, var(--white) 6%, transparent) 0 6px,
		transparent 6px 12px
	);
}

.ent.enemyhit {
	top: 26px;
	border-color: color-mix(in srgb, var(--enemy-accent) 90%, transparent);
	background: linear-gradient(
		180deg,
		color-mix(in srgb, var(--enemy-accent) 52%, var(--surface)),
		color-mix(in srgb, var(--enemy-accent) 34%, var(--surface))
	);
	color: color-mix(in srgb, var(--enemy-accent) 12%, var(--white));
	box-shadow:
		0 2px 10px color-mix(in srgb, var(--black) 45%, transparent),
		0 0 14px -4px color-mix(in srgb, var(--enemy-accent) 70%, transparent);

	.tip {
		background: var(--enemy-accent);
		box-shadow: 0 0 8px var(--enemy-accent);
	}
}

.ent.enemychannel {
	top: 26px;
	border-color: var(--enemy-accent);
	background: linear-gradient(
		180deg,
		color-mix(in srgb, var(--enemy-accent) 42%, var(--surface)),
		color-mix(in srgb, var(--enemy-accent) 24%, var(--surface))
	);
	color: color-mix(in srgb, var(--enemy-accent) 12%, var(--white));
	letter-spacing: 0.04em;
	box-shadow:
		0 0 22px -2px color-mix(in srgb, var(--enemy-accent) 85%, transparent),
		inset 0 0 18px color-mix(in srgb, var(--enemy-accent) 25%, transparent);

	.ecl {
		position: relative;
		z-index: 1;
		font-weight: 700;
	}
	.tip {
		background: var(--white);
		box-shadow:
			0 0 10px var(--white),
			0 0 18px var(--enemy-accent);
	}
}

.ent.attack {
	top: 118px;
	border-color: color-mix(in srgb, var(--log-enemy) 88%, transparent);
	background: linear-gradient(
		180deg,
		color-mix(in srgb, var(--log-enemy) 46%, var(--surface)),
		color-mix(in srgb, var(--log-enemy) 30%, var(--surface))
	);
	color: color-mix(in srgb, var(--log-enemy) 12%, var(--white));
	box-shadow: 0 2px 10px color-mix(in srgb, var(--black) 45%, transparent);

	.tip {
		background: color-mix(in srgb, var(--log-enemy) 60%, var(--white));
		box-shadow: 0 0 8px color-mix(in srgb, var(--log-enemy) 70%, transparent);
	}
}

.ent.channel {
	top: 118px;
	border-color: color-mix(in srgb, var(--rarity-epic) 92%, transparent);
	background: linear-gradient(
		180deg,
		color-mix(in srgb, var(--rarity-epic) 50%, var(--surface)),
		color-mix(in srgb, var(--rarity-epic) 32%, var(--surface))
	);
	color: color-mix(in srgb, var(--rarity-epic) 12%, var(--white));
	box-shadow:
		0 2px 10px color-mix(in srgb, var(--black) 45%, transparent),
		0 0 14px -4px color-mix(in srgb, var(--rarity-epic) 60%, transparent);

	.tip {
		background: var(--rarity-epic);
		box-shadow: 0 0 8px var(--rarity-epic);
	}
}

/* defensive cover spans (76px tall, top-aligned, translucent) */
.ent.block {
	top: 8px;
	height: 76px;
	z-index: 2;
	align-items: flex-start;
	padding-top: 3px;
	border-style: dashed;
	border-color: color-mix(in srgb, var(--health-remaining-color) 50%, transparent);
	background: color-mix(in srgb, var(--health-remaining-color) 12%, transparent);
	box-shadow: inset 0 0 16px color-mix(in srgb, var(--health-remaining-color) 12%, transparent);
	color: color-mix(in srgb, var(--health-remaining-color) 35%, var(--white));
	font-size: 10px;
	letter-spacing: 0.12em;
}

.ent.enemyblock {
	top: 100px;
	height: 76px;
	z-index: 2;
	align-items: flex-start;
	padding-top: 3px;
	border-style: dashed;
	border-color: color-mix(in srgb, var(--enemy-accent) 50%, transparent);
	color: var(--log-enemy);
	background: repeating-linear-gradient(
		45deg,
		color-mix(in srgb, var(--enemy-accent) 13%, transparent) 0 6px,
		color-mix(in srgb, var(--enemy-accent) 4%, transparent) 6px 12px
	);
	font-size: 10px;
	letter-spacing: 0.12em;
}

.ent.resolved {
	opacity: 0.2;
	filter: saturate(0.4);
}
.ent.cancelled {
	opacity: 0.3;
	text-decoration: line-through;
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
	border-color: color-mix(in srgb, var(--health-remaining-color) 70%, transparent);
	background: color-mix(in srgb, var(--health-remaining-color) 10%, transparent);
	color: color-mix(in srgb, var(--health-remaining-color) 35%, var(--white));
}

.preview.attack {
	top: 118px;
	border-color: color-mix(in srgb, var(--log-enemy) 60%, transparent);
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

/* ── banner ────────────────────────────────────────────────────────────── */
.banner {
	position: absolute;
	inset: 0;
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	gap: 6px;
	z-index: 12;
	background: color-mix(in srgb, var(--surface) 82%, transparent);
	backdrop-filter: blur(3px);
	font-family: var(--sans);

	h2 {
		font-size: 38px;
		margin: 0;
		letter-spacing: 0.06em;
		font-weight: 700;
	}

	.bsub {
		font-family: var(--mono);
		font-size: 12px;
		color: var(--text-tertiary);
	}

	&.win h2 {
		color: var(--health-remaining-color);
		text-shadow: 0 0 24px color-mix(in srgb, var(--health-remaining-color) 60%, transparent);
	}
	&.lose h2 {
		color: var(--enemy-accent);
		text-shadow: 0 0 24px color-mix(in srgb, var(--enemy-accent) 60%, transparent);
	}
}
</style>
