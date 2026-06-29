<!-- The floating combat-number layer over a battler card. Subscribes to the engine's per-activation
     combat events and, for the events striking this card's side, spawns a short-lived number/label
     that pops and drifts up. The number is tinted by its damage type (a typed, non-physical plain hit
     also shows the type glyph); crit/dodge keep their own outcome icon, and an absorbed hit shows
     its net heal in the regen hue (#1320, Area F). Purely presentational (aria-hidden) — the combat log
     is the accessible record of the same events. -->
<div class="floaters" aria-hidden="true" data-testid={testId} style:--float-duration="{DURATION_MS}ms">
	{#each floaters as floater (floater.id)}
		<div
			class="floater"
			class:crit={floater.crit}
			style:left="{floater.x}%"
			style:color={floater.color}
			style:font-size="{floater.size}px"
		>
			{#if floater.glyph}
				<span class="floater-glyph"
					><DamageTypeGlyph glyph={floater.glyph} color={floater.color} size={Math.round(floater.size * 0.82)} /></span
				>
			{:else if floater.icon}
				<img class="floater-icon" src={floater.icon} alt="" />
			{/if}
			{#if floater.amount}<span>{floater.amount}</span>{/if}
			{#if floater.label}<span class="floater-label">{floater.label}</span>{/if}
		</div>
	{/each}
</div>

<script lang="ts">
import { onMount } from 'svelte';
import { formatNum, damageTypeColor, damageTypeGlyph, type DamageGlyph } from '$lib/common';
import { onCombatFloat, type CombatFloatEvent } from '$lib/engine';
import { EDamageType } from '$lib/api';
import DamageTypeGlyph from '$components/DamageTypeGlyph.svelte';

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
	/** Inline damage-type glyph for a typed (non-physical) plain hit; crit/dodge use `icon` instead. */
	glyph?: DamageGlyph;
	icon: string;
	amount: string;
	label: string;
}

/** Clean per-outcome icon art (in `static/img`) for the crit/dodge floaters; a plain hit shows its
 *  damage-type glyph instead (or nothing, for physical). These reuse the clean base symbols — the crit
 *  magnitude attribute icon plus the standalone `Dodge` — so the popup and the attribute display share
 *  one visual language. */
const FLOAT_ICON: Partial<Record<CombatFloatEvent['kind'], string>> = {
	crit: '/img/Critical Damage.png',
	dodge: '/img/Dodge.png'
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

/** Whether the event is an absorbed hit's net heal (a negative amount), which renders as a positive
 *  heal in the regen hue rather than a damage number. */
const isHeal = (event: CombatFloatEvent): boolean => event.amount !== undefined && event.amount < 0;

/** Fallback colour for an event without a damage type (a dodge, or a live-preview sample). */
const colorFor = (event: CombatFloatEvent): string => {
	switch (event.kind) {
		case 'crit':
			return 'var(--gold)';
		case 'dodge':
			return 'var(--text-secondary)';
		default:
			// A player hit lands on the enemy (brand accent); an incoming enemy hit on the player (enemy hue).
			return event.target === 'enemy' ? 'var(--accent)' : 'var(--enemy-accent)';
	}
};

/** The floater's number colour: the regen hue for an absorbed heal, otherwise the damage-type hue
 *  (falling back to the per-outcome colour when no type is present, e.g. a dodge). */
const colorOf = (event: CombatFloatEvent): string => {
	if (isHeal(event)) {
		return 'var(--health-remaining-color)';
	}
	return event.damageType !== undefined ? damageTypeColor(event.damageType) : colorFor(event);
};

/** A plain hit's damage-type glyph — shown only for a typed (non-physical) hit so basic attacks stay
 *  clean and crit/dodge keep their own outcome icon. Physical is the untyped baseline. */
const glyphOf = (event: CombatFloatEvent): DamageGlyph | undefined =>
	event.kind === 'hit' && event.damageType !== undefined && event.damageType !== EDamageType.Physical
		? damageTypeGlyph(event.damageType)
		: undefined;

/** The number shown: an absorbed heal as `+N`, a dodge/no-amount event as nothing, else the damage. */
const amountOf = (event: CombatFloatEvent): string => {
	if (event.amount === undefined) {
		return '';
	}
	return isHeal(event) ? `+${formatNum(-event.amount)}` : formatNum(event.amount);
};

const labelFor = (kind: CombatFloatEvent['kind']): string => {
	switch (kind) {
		case 'crit':
			return 'CRIT';
		case 'dodge':
			return 'DODGE';
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
		color: colorOf(event),
		size: crit ? 30 : 21,
		crit,
		glyph: glyphOf(event),
		icon: FLOAT_ICON[event.kind] ?? '',
		amount: amountOf(event),
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

// Per-outcome combat icon (crit/dodge); sized in em so it tracks the floater's font-size.
.floater-icon {
	width: 1.15em;
	height: 1.15em;
	margin-right: -0.22em;
	vertical-align: -0.22em;
	object-fit: contain;
}

// Inline damage-type glyph for a typed plain hit; aligned to sit on the number's baseline like the icon.
.floater-glyph {
	display: inline-flex;
	margin-right: -0.1em;
	vertical-align: -0.16em;
}

.floater-label {
	font-size: 0.7em;
	letter-spacing: 0.13em;
	margin-left: -0.12em;
	font-weight: 600;
	opacity: 0.95;
}
</style>
