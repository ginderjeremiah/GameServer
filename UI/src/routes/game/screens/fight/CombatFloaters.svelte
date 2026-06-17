<!-- The floating combat-number layer over a battler card. Subscribes to the engine's per-activation
     combat events and, for the events striking this card's side, spawns a short-lived number/label
     that pops and drifts up. Colour and label are coded by outcome (crit/dodge/block/hit). Purely
     presentational (aria-hidden) — the combat log is the accessible record of the same events. -->
<div class="floaters" aria-hidden="true" data-testid={testId}>
	{#each floaters as floater (floater.id)}
		<div
			class="floater"
			class:crit={floater.crit}
			style:left="{floater.x}%"
			style:color={floater.color}
			style:font-size="{floater.size}px"
		>
			<span class="floater-icon"></span>
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
	amount: string;
	label: string;
}

/** Matches the float animation length in `common.scss` (`dmg-rise`), with a small grace margin. */
const DURATION_MS = 1500;

let floaters = $state<Floater[]>([]);
let nextId = 0;

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
	const labelOnly = event.amount === undefined;
	const id = nextId++;
	floaters.push({
		id,
		x: 24 + Math.random() * 52,
		color: colorFor(event),
		size: crit ? 30 : labelOnly ? 20 : 21,
		crit,
		amount: event.amount === undefined ? '' : formatNum(event.amount),
		label: labelFor(event.kind)
	});
	setTimeout(() => {
		floaters = floaters.filter((floater) => floater.id !== id);
	}, DURATION_MS + 80);
};

// Subscribe client-side only (the engine emits nothing during SSR); onMount's return is the cleanup.
onMount(() => onCombatFloat(spawn));
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
	animation: dmg-rise 1500ms cubic-bezier(0.18, 0.7, 0.28, 1) forwards;

	&.crit {
		text-shadow:
			0 1px 4px color-mix(in srgb, var(--black) 85%, transparent),
			0 0 10px currentColor;
	}
}

// Placeholder for the per-outcome combat icons that will drop in later.
.floater-icon {
	display: inline-block;
	width: 0.62em;
	height: 0.62em;
	border: 1px solid currentColor;
	border-radius: 2px;
	opacity: 0.5;
	margin-right: 0.26em;
	vertical-align: 0.02em;
}

.floater-label {
	font-size: 0.6em;
	letter-spacing: 0.13em;
	margin-left: 0.32em;
	font-weight: 600;
	opacity: 0.95;
}
</style>
