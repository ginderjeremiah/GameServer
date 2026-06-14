<!-- A pointer-drag surface, not a keyboard control: dragging to aim has no keyboard analogue and
	 casting is keyboard-reachable via the global 1–7 hotkeys, so it claims no button role. -->
<!-- svelte-ignore a11y_no_static_element_interactions -->
<div
	class="card"
	class:lane-b={card.kind === 'block'}
	class:skill={card.skill}
	onpointerdown={(e) => {
		e.preventDefault();
		view.beginDrag(index, card.key, e.clientX, e.clientY);
	}}
	onmouseenter={() => view.setHover(card.key)}
	onmouseleave={() => view.clearHover(card.key)}
>
	<div class="slotnum">{slot}</div>
	<div class="t">{card.label}</div>
	<div class="type"><span class="d"></span>{card.kind}</div>
	<div class="art"><span>art</span></div>
	<div class="meta">{card.meta}</div>
</div>

<script lang="ts">
import type { CardDef } from '$lib/card-game';
import type { CardGameView } from '../card-game-view.svelte';

interface Props {
	card: CardDef;
	index: number;
	/** 1-based hotkey slot number shown in the keycap. */
	slot: number;
	view: CardGameView;
}
const { card, index, slot, view }: Props = $props();
</script>

<style lang="scss">
.card {
	width: 100px;
	height: 138px;
	border: 1px solid var(--border-medium);
	border-left: 3px solid var(--attr-strength);
	border-radius: 10px;
	background: linear-gradient(180deg, color-mix(in srgb, var(--white) 7%, var(--surface)), var(--panel-2));
	box-shadow: 0 8px 20px -10px color-mix(in srgb, var(--black) 80%, transparent);
	cursor: grab;
	padding: 9px 9px 8px;
	display: flex;
	flex-direction: column;
	gap: 6px;
	position: relative;
	user-select: none;
	// Keep a touch press-drag driving the cast/aim gesture instead of scrolling the page.
	touch-action: none;
	transition:
		transform 0.12s ease,
		box-shadow 0.12s ease,
		border-color 0.12s ease;
	overflow: hidden;
	--card-glow: color-mix(in srgb, var(--attr-strength) 50%, transparent);
}

.card::before {
	content: '';
	position: absolute;
	inset: 0;
	background: radial-gradient(120px 60px at 50% 0%, color-mix(in srgb, var(--white) 5%, transparent), transparent 70%);
	pointer-events: none;
}

.card:hover {
	transform: translateY(-8px);
	box-shadow:
		0 16px 30px -12px color-mix(in srgb, var(--black) 85%, transparent),
		0 0 18px -6px var(--card-glow);
	border-color: var(--border-medium);
}

.card:active {
	cursor: grabbing;
}

.t {
	font-family: var(--sans);
	font-weight: 600;
	font-size: 15px;
	letter-spacing: -0.01em;
	color: color-mix(in srgb, var(--attr-strength) 35%, var(--text-primary));
}

.type {
	display: flex;
	align-items: center;
	gap: 5px;
	font-family: var(--mono);
	font-size: 8.5px;
	letter-spacing: 0.12em;
	text-transform: uppercase;
	color: var(--text-muted);
}

.type .d {
	width: 6px;
	height: 6px;
	transform: rotate(45deg);
	background: var(--attr-strength);
}

.art {
	flex: 1;
	border: 1px solid var(--border-subtle);
	border-radius: 5px;
	background: repeating-linear-gradient(
		45deg,
		color-mix(in srgb, var(--white) 3%, transparent) 0 6px,
		transparent 6px 12px
	);
	display: flex;
	align-items: center;
	justify-content: center;

	span {
		font-family: var(--mono);
		font-size: 8px;
		letter-spacing: 0.14em;
		text-transform: uppercase;
		color: var(--text-muted);
	}
}

.meta {
	font-family: var(--mono);
	font-size: 10px;
	color: var(--text-tertiary);
	text-align: center;
}

.slotnum {
	position: absolute;
	top: 6px;
	right: 7px;
	font-family: var(--mono);
	font-size: 9px;
	font-weight: 700;
	color: var(--text-tertiary);
	border: 1px solid var(--border-medium);
	border-radius: 4px;
	padding: 0 4px;
	line-height: 1.45;
	background: color-mix(in srgb, var(--white) 4%, transparent);
}

/* block cards (Guard / Dodge) */
.card.lane-b {
	border-left-color: var(--health-remaining-color);
	--card-glow: color-mix(in srgb, var(--health-remaining-color) 50%, transparent);

	.type .d {
		background: var(--health-remaining-color);
	}
	.t {
		color: color-mix(in srgb, var(--health-remaining-color) 45%, var(--text-primary));
	}
}

/* rare skill cards (Channel) */
.card.skill {
	border-left-color: var(--rarity-epic);
	--card-glow: color-mix(in srgb, var(--rarity-epic) 55%, transparent);

	.type .d {
		background: var(--rarity-epic);
	}
	.t {
		color: color-mix(in srgb, var(--rarity-epic) 45%, var(--text-primary));
	}
}
</style>
