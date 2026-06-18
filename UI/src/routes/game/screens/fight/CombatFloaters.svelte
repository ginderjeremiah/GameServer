<!-- The floating combat-number layer over a battler card. Subscribes to the engine's per-activation
     combat events and, for the events striking this card's side, spawns a short-lived number/label
     that pops and drifts up. Colour and label are coded by outcome (crit/dodge/block/hit). Purely
     presentational (aria-hidden) — the combat log is the accessible record of the same events. -->
<div class="floaters" aria-hidden="true" data-testid={testId} style:--float-duration="{DURATION_MS}ms">
	{#each floaters as floater (floater.id)}
		<div
			class="floater"
			class:crit={floater.crit}
			style:left="{floater.x}%"
			style:color={floater.color}
			style:font-size="{floater.size}px"
		>
			{#if floater.icon}<img class="floater-icon" src={floater.icon} alt="" />{/if}
			{#if floater.amount}<span>{floater.amount}</span>{/if}
			{#if floater.label}<span class="floater-label">{floater.label}</span>{/if}
		</div>
	{/each}
</div>

<script lang="ts">
import { onMount } from 'svelte';
import { formatNum } from '$lib/common';
import { onCombatFloat, type CombatFloatEvent } from '$lib/engine';

type Props = {
	/** Which card this layer sits over; only events targeting this side spawn here. */
	side: 'player' | 'enemy';
	testId?: string;
};

const { side, testId }: Props = $props();

interface Floater {
	id: number;
	x: number;
	color: string;
	size: number;
	crit: boolean;
	icon: string;
	amount: string;
	label: string;
}

/** Clean per-outcome icon art (in `static/img`) for the crit/dodge/block floaters; a plain hit has
 *  none. These reuse the clean base symbols — the crit/block magnitude attribute icons plus the
 *  standalone `Dodge` — so the popup and the attribute display share one visual language. */
const FLOAT_ICON: Partial<Record<CombatFloatEvent['kind'], string>> = {
	crit: '/img/Critical Damage.png',
	dodge: '/img/Dodge.png',
	block: '/img/Block Reduction.png'
};

/** Drives both the JS removal timer and the CSS `dmg-rise` animation (via the `--float-duration`
 *  custom property) so the two can't drift; the timer adds a small grace margin over the animation. */
const DURATION_MS = 1500;

let floaters = $state<Floater[]>([]);
let nextId = 0;
// Pending removal timers, tracked so they can be cleared on unmount (no write-after-destroy).
// Plain Set — pure bookkeeping that's never read reactively (mirrors ToastContainer's removalTimers).
// eslint-disable-next-line svelte/prefer-svelte-reactivity
const removalTimers = new Set<ReturnType<typeof setTimeout>>();

const colorFor = (event: CombatFloatEvent): string => {
	switch (event.kind) {
		case 'crit':
			return 'var(--gold)';
		case 'dodge':
			return 'var(--text-secondary)';
		case 'block':
			return 'var(--block-color)';
		default:
			// A player hit lands on the enemy (brand accent); an incoming enemy hit on the player (enemy hue).
			return event.target === 'enemy' ? 'var(--accent)' : 'var(--enemy-accent)';
	}
};

const labelFor = (kind: CombatFloatEvent['kind']): string => {
	switch (kind) {
		case 'crit':
			return 'CRIT';
		case 'dodge':
			return 'DODGE';
		case 'block':
			return 'BLOCK';
		default:
			return '';
	}
};

const spawn = (event: CombatFloatEvent) => {
	if (event.target !== side) {
		return;
	}
	const crit = event.kind === 'crit';
	const id = nextId++;
	floaters.push({
		id,
		x: 24 + Math.random() * 52,
		color: colorFor(event),
		size: crit ? 30 : 21,
		crit,
		icon: FLOAT_ICON[event.kind] ?? '',
		amount: event.amount === undefined ? '' : formatNum(event.amount),
		label: labelFor(event.kind)
	});
	const timer = setTimeout(() => {
		removalTimers.delete(timer);
		floaters = floaters.filter((floater) => floater.id !== id);
	}, DURATION_MS + 80);
	removalTimers.add(timer);
};

// Subscribe client-side only (the engine emits nothing during SSR); the cleanup unsubscribes and
// clears any pending removal timers so none fire (and write the array) after the component is gone.
onMount(() => {
	const unsubscribe = onCombatFloat(spawn);
	return () => {
		unsubscribe();
		for (const timer of removalTimers) {
			clearTimeout(timer);
		}
		removalTimers.clear();
	};
});
</script>

<style lang="scss">
.floaters {
	position: absolute;
	inset: -12px 0 0 0;
	pointer-events: none;
	overflow: visible;
	z-index: 6;
}

.floater {
	position: absolute;
	top: 54%;
	font-family: var(--mono);
	font-weight: 700;
	white-space: nowrap;
	text-shadow: 0 1px 4px color-mix(in srgb, var(--black) 85%, transparent);
	animation: dmg-rise var(--float-duration) cubic-bezier(0.18, 0.7, 0.28, 1) forwards;

	&.crit {
		text-shadow:
			0 1px 4px color-mix(in srgb, var(--black) 85%, transparent),
			0 0 6px currentColor;
	}
}

// Per-outcome combat icon (crit/dodge/block); sized in em so it tracks the floater's font-size.
.floater-icon {
	width: 1.15em;
	height: 1.15em;
	margin-right: -0.22em;
	vertical-align: -0.22em;
	object-fit: contain;
}

.floater-label {
	font-size: 0.7em;
	letter-spacing: 0.13em;
	margin-left: -0.12em;
	font-weight: 600;
	opacity: 0.95;
}
</style>
