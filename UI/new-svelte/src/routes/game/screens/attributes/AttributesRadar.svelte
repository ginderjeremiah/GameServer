<svg width={size} height={size} viewBox="0 0 {size} {size}" class="radar" role="img" aria-label="Attribute build radar">
	{#each rings as f (f)}
		<polygon points={ringPoints(f)} class="ring" />
	{/each}

	{#each spokes as spoke, i (i)}
		<line x1={C} y1={C} x2={spoke[0]} y2={spoke[1]} class="spoke" />
	{/each}

	<polygon points={shapePoints(savedDisp)} class="saved-shape" />
	<polygon points={shapePoints(disp)} class="current-shape" class:big />

	{#each vertices as vertex, i (i)}
		{@const clickable = interactive && view.canInc()}
		<g
			class="vertex"
			class:clickable
			role="button"
			tabindex={clickable ? 0 : -1}
			aria-label="Add a point to {attributeName(coreIds[i])}"
			onclick={() => clickable && view.inc(i)}
			onkeydown={(e) => clickable && handleKey(e, i)}
		>
			<circle cx={vertex[0]} cy={vertex[1]} r="11" fill="transparent" />
			<circle cx={vertex[0]} cy={vertex[1]} r={big ? 4.5 : 3.5} class="dot" style="--c: {attributeColor(coreIds[i])}" />
		</g>
	{/each}

	{#each labels as l, i (i)}
		<g>
			<text
				x={l.lx}
				y={l.ly - (big ? 4 : 3)}
				text-anchor="middle"
				class="code"
				class:big
				style="fill: {attributeColor(l.id)}"
			>
				{attributeCode(l.id)}
			</text>
			<text x={l.lx} y={l.ly + (big ? 11 : 9)} text-anchor="middle" class="val" class:big>
				{view.values[i]}
			</text>
		</g>
	{/each}
</svg>

<script lang="ts">
import { untrack } from 'svelte';
import { attributeColor, attributeCode } from '$lib/common';
import { CORE_ATTRIBUTES, attributeName, type AttributesView } from './attributes-view.svelte';

interface Props {
	view: AttributesView;
	size?: number;
	interactive?: boolean;
}

const { view, size = 430, interactive = true }: Props = $props();

const coreIds = CORE_ATTRIBUTES;
const rings = [0.25, 0.5, 0.75, 1];

const C = $derived(size / 2);
const R = $derived(size / 2 - (size > 340 ? 60 : 42));
const big = $derived(size > 340);

const ang = (i: number): number => ((-90 + i * 60) * Math.PI) / 180;
const pt = (i: number, r: number): [number, number] => [C + Math.cos(ang(i)) * r, C + Math.sin(ang(i)) * r];

const ringPoints = (f: number): string => coreIds.map((_, i) => pt(i, R * f).join(',')).join(' ');
const shapePoints = (vals: number[]): string => vals.map((v, i) => pt(i, (v / view.hexMax) * R).join(',')).join(' ');

// Animated display values give the build shape a smooth morph as points are
// spent. Reduced-motion preferences snap straight to the target.
const targets = $derived(view.values.map((v) => Math.min(v, view.hexMax)));
// Seeded once from the current build so the first paint is already correct; the
// effect below then animates it toward `targets`.
let disp = $state<number[]>(untrack(() => view.values.map((v) => Math.min(v, view.hexMax))));
let raf = 0;

$effect(() => {
	const target = targets;
	cancelAnimationFrame(raf);
	// Read the live display values without tracking them: writing `disp` (here or
	// in the animation frames) must never re-trigger this effect, which depends
	// only on `targets`. Tracking `disp` here would make it feed itself and loop.
	const current = untrack(() => disp);
	if (current.length !== target.length || prefersReducedMotion()) {
		disp = [...target];
		return;
	}
	const tick = () => {
		let moving = false;
		const next = disp.map((c, i) => {
			const d = target[i] - c;
			if (Math.abs(d) < 0.01) {
				return target[i];
			}
			moving = true;
			return c + d * 0.18;
		});
		disp = next;
		if (moving) {
			raf = requestAnimationFrame(tick);
		}
	};
	raf = requestAnimationFrame(tick);
	return () => cancelAnimationFrame(raf);
});

const savedDisp = $derived(view.savedValues.map((v) => Math.min(v, view.hexMax)));
const spokes = $derived(coreIds.map((_, i) => pt(i, R)));
const vertices = $derived(disp.map((v, i) => pt(i, (v / view.hexMax) * R)));
const labels = $derived(
	coreIds.map((id, i) => {
		const [lx, ly] = pt(i, R + (big ? 30 : 22));
		return { id, lx, ly };
	})
);

function handleKey(e: KeyboardEvent, i: number): void {
	if (e.key === 'Enter' || e.key === ' ') {
		e.preventDefault();
		view.inc(i);
	}
}

function prefersReducedMotion(): boolean {
	return (
		typeof window !== 'undefined' &&
		!!window.matchMedia &&
		window.matchMedia('(prefers-reduced-motion: reduce)').matches
	);
}
</script>

<style lang="scss">
.radar {
	overflow: visible;
	flex-shrink: 0;
}

.ring {
	fill: none;
	stroke: color-mix(in srgb, var(--white) 7%, transparent);
	stroke-width: 1;
}

.spoke {
	stroke: color-mix(in srgb, var(--white) 7%, transparent);
	stroke-width: 1;
}

.saved-shape {
	fill: color-mix(in srgb, var(--text-primary) 4%, transparent);
	stroke: color-mix(in srgb, var(--text-primary) 32%, transparent);
	stroke-width: 1.2;
	stroke-dasharray: 3 3;
}

.current-shape {
	fill: color-mix(in srgb, var(--accent) 13%, transparent);
	stroke: var(--accent);
	stroke-width: 1.5;
	filter: drop-shadow(0 0 8px color-mix(in srgb, var(--accent) 40%, transparent));

	&.big {
		stroke-width: 1.8;
	}
}

.vertex {
	outline: none;

	&.clickable {
		cursor: pointer;
	}

	&:focus-visible .dot {
		stroke: var(--accent);
		stroke-width: 2.5;
	}
}

.dot {
	fill: var(--c);
	stroke: var(--page);
	stroke-width: 1.5;
	filter: drop-shadow(0 0 5px var(--c));
}

.code {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1px;

	&.big {
		font-size: 11px;
	}
}

.val {
	font-family: var(--sans);
	font-size: 11px;
	font-weight: 600;
	fill: var(--text-primary);

	&.big {
		font-size: 13px;
	}
}
</style>
