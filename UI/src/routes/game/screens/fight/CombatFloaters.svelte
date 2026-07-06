<!-- The floating combat-number layer over a battler card. Subscribes to the engine's per-activation
     combat events and, for the events striking this card's side, spawns a short-lived number/label
     that pops and drifts up. The number is tinted by its damage type (a typed, non-physical plain hit
     also shows the type glyph); crit/dodge keep their own outcome icon, and an absorbed hit shows
     its net heal in the regen hue (#1320, Area F). A reflect (#1330) floats over the original attacker
     with the shared combat-log reflect glyph and the side's hue — raw/untyped, never tinted by type.
     Purely presentational (aria-hidden) — the combat log is the accessible record of the same events. -->
<div class="floaters" aria-hidden="true" data-testid={testId} style:--float-duration="{DURATION_MS}ms">
	{#each floaters as floater (floater.id)}
		<div
			class="floater"
			class:crit={floater.crit}
			style:left="{floater.x}%"
			style:color={floater.color}
			style:font-size="{floater.size}px"
		>
			<span class="floater-main">
				{#if floater.glyph}
					<span class="floater-glyph"
						><LogGlyph glyph={floater.glyph} color={floater.color} size={Math.round(floater.size * 0.85)} /></span
					>
				{:else if floater.icon}
					<img class="floater-icon" src={floater.icon} alt="" />
				{/if}
				{#if floater.amount}<span>{floater.amount}</span>{/if}
				{#if floater.label}<span class="floater-label">{floater.label}</span>{/if}
			</span>
			<!-- A multi-typed hit (#1343) shows the exact split as a thin ratio bar under the primary-coloured
			     number; a single-typed hit stays a clean number, exactly as before. -->
			{#if floater.portions.length > 1}
				<div class="floater-ratio"><DamageRatioBar portions={floater.portions} height={3} /></div>
			{/if}
		</div>
	{/each}
</div>

<script lang="ts">
import { onMount } from 'svelte';
import { formatNum, damageTypeColor, damageTypeIcon } from '$lib/common';
import { onCombatFloat, type CombatFloatEvent } from '$lib/engine';
import { EDamageType, type ISkillDamagePortion } from '$lib/api';
import LogGlyph from '$components/log-panel/LogGlyph.svelte';
import DamageRatioBar from '$components/DamageRatioBar.svelte';
import type { GlyphKind } from '$components/log-panel/log-kind';

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
	/** Per-outcome art for a crit/dodge, or a typed (non-physical) plain hit's damage-type icon; empty
	 *  for a plain physical hit. */
	icon: string;
	/** The shared combat-log glyph for a reflect (#1330); empty for every other kind, which use {@link icon}. */
	glyph: GlyphKind | '';
	amount: string;
	label: string;
	/** The hit's weighted leaf-type split (#1343); rendered as a ratio bar only for a multi-typed hit
	 *  (2+ portions), empty otherwise so a single-typed hit stays a clean number. */
	portions: readonly ISkillDamagePortion[];
}

/** Clean per-outcome icon art (in `static/img`) for the crit/dodge floaters; a plain hit shows its
 *  damage-type icon instead (or nothing, for physical). These reuse the clean base symbols — the crit
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

/** Fallback colour for an event without a damage type (a dodge/parry, or a live-preview sample). */
const colorFor = (event: CombatFloatEvent): string => {
	switch (event.kind) {
		case 'crit':
			return 'var(--gold)';
		case 'dodge':
		case 'parry':
			return 'var(--text-secondary)';
		default:
			// A player hit/reflect lands on the enemy (brand accent); an incoming enemy hit, or a reflect the
			// enemy returned, lands on the player (enemy hue). The reflect hue thus mirrors the combat-log line's.
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

/** The floater's icon: a crit/dodge's per-outcome art, otherwise a typed (non-physical) plain hit's
 *  damage-type icon so basic/physical attacks stay clean. Physical is the untyped baseline. An absorbed
 *  heal shows no icon — it's not the incoming attack's damage type, it's a heal. */
const iconOf = (event: CombatFloatEvent): string => {
	if (isHeal(event)) {
		return '';
	}
	const outcome = FLOAT_ICON[event.kind];
	if (outcome) {
		return outcome;
	}
	return event.kind === 'hit' && event.damageType !== undefined && event.damageType !== EDamageType.Physical
		? damageTypeIcon(event.damageType)
		: '';
};

/** The shared combat-log reflect glyph for a reflect float (#1330), so the returned-damage popup reads
 *  with the same symbol as its log line; empty for every other kind (which use a PNG {@link iconOf}). */
const glyphOf = (event: CombatFloatEvent): GlyphKind | '' => (event.kind === 'reflect' ? 'reflect' : '');

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
		// No dedicated art yet (like the DoT accumulators' attribute icons), so a parry floats as a
		// label-only popup; the riposte that follows floats as a normal hit/crit number.
		case 'parry':
			return 'PARRY';
		case 'reflect':
			return 'REFLECT';
		default:
			return '';
	}
};

const spawn = (event: CombatFloatEvent) => {
	if (event.target !== side) {
		return;
	}
	// The tick worker keeps the sim running at full rate while the tab is hidden (#1594), so a long
	// hidden battle can spawn floaters far faster than the throttled removal timer clears them.
	// They're purely presentational (aria-hidden) and nobody is watching a hidden tab, so skip the
	// spawn entirely rather than let the DOM accumulate for a stutter on refocus.
	if (document.hidden) {
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
		icon: iconOf(event),
		glyph: glyphOf(event),
		amount: amountOf(event),
		label: labelFor(event.kind),
		portions: isHeal(event) ? [] : (event.portions ?? [])
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
	display: flex;
	flex-direction: column;
	align-items: center;
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

// The number line (icon/glyph + amount + label) on its own row, so the ratio bar can stack beneath it.
// Kept a plain inline-content block (not flex) so the icon/glyph `vertical-align` baseline tweaks hold.
.floater-main {
	display: block;
}

// The multi-typed split bar, sized to roughly the number's width and centred beneath it.
.floater-ratio {
	width: 80%;
	min-width: 24px;
	margin-top: 2px;
}

// Per-outcome / damage-type combat icon; sized in em so it tracks the floater's font-size.
.floater-icon {
	width: 1.15em;
	height: 1.15em;
	margin-right: -0.22em;
	vertical-align: -0.22em;
	object-fit: contain;
}

// Inline wrapper for the reflect glyph (a stroke SVG, not a PNG): aligns it to the number's baseline
// the same way .floater-icon does for the bitmap icons.
.floater-glyph {
	display: inline-flex;
	margin-right: 0.1em;
	vertical-align: -0.18em;
}

.floater-label {
	font-size: 0.7em;
	letter-spacing: 0.13em;
	margin-left: -0.12em;
	font-weight: 600;
	opacity: 0.95;
}
</style>
