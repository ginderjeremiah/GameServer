<svg
	bind:this={svgEl}
	width={size}
	height={size}
	viewBox="0 0 {size} {size}"
	class="radar"
	role="img"
	aria-label="Attribute build radar"
>
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
			class:interactive
			class:dragging={dragIndex === i}
			role="button"
			tabindex={clickable ? 0 : -1}
			aria-label={clickable
				? `Adjust ${attributeName(coreIds[i], staticData.attributes)} — drag to allocate or click to add a point`
				: `Adjust ${attributeName(coreIds[i], staticData.attributes)} — drag to refund points`}
			onpointerdown={(e) => startDrag(e, i)}
			onclick={() => onVertexClick(i)}
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
import { onMount, untrack } from 'svelte';
import { attributeColor, attributeCode, attributeName, prefersReducedMotion } from '$lib/common';
import { staticData } from '$stores';
import { CORE_ATTRIBUTES, radarValueAtPointer, type AttributesView } from './attributes-view.svelte';

interface Props {
	view: AttributesView;
	size?: number;
	interactive?: boolean;
}

const { view, size = 430, interactive = true }: Props = $props();

const coreIds = CORE_ATTRIBUTES;
const rings = [0.25, 0.5, 0.75, 1];

let svgEl: SVGSVGElement | undefined;
// The axis currently being dragged (null when idle), and whether the active
// gesture has crossed the movement dead-zone — a moved gesture ends in a
// synthetic click we must ignore so a drag isn't also counted as the quick +1
// click. `dragStart` is the pointerdown position the dead-zone is measured from.
let dragIndex = $state<number | null>(null);
let dragMoved = false;
let dragStartX = 0;
let dragStartY = 0;

// A press only becomes a drag once it travels past this many CSS pixels from the
// pointerdown point. Below it, the gesture stays a tap — so the finger jitter a
// real tap carries can't suppress the +1 click or refund a point.
const DRAG_THRESHOLD_PX = 5;

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

/* ── drag-to-allocate ──────────────────────────────────────────────────────
   Dragging a vertex radially sets its attribute directly: outward allocates
   points (up to the remaining budget), inward refunds them (down to 0). The
   move/up listeners live on the window so the gesture keeps tracking even when
   the pointer leaves the small vertex hit-area. */
function startDrag(e: PointerEvent, i: number): void {
	if (!interactive) {
		return;
	}
	dragIndex = i;
	dragMoved = false;
	dragStartX = e.clientX;
	dragStartY = e.clientY;
	// Pin the radar scale for the gesture: the scale grows with the build, so an
	// unpinned scale would rescale mid-drag and inflate the value under the
	// pointer, over-allocating near the boundary values (#433).
	view.lockScale();
	// Suppress the native text/SVG selection a press-drag would otherwise start.
	e.preventDefault();
}

function onVertexClick(i: number): void {
	// A drag ends with a synthetic click; only a genuine, motion-free click counts
	// as the quick +1 affordance.
	if (dragMoved) {
		dragMoved = false;
		return;
	}
	if (interactive && view.canInc()) {
		view.inc(i);
	}
}

/** The attribute value the dragged axis should take for a pointer at the given
 *  client coordinates, mapping screen space into the SVG's user space. */
function valueAtClient(clientX: number, clientY: number, i: number): number {
	const rect = svgEl?.getBoundingClientRect();
	if (!rect) {
		return view.values[i];
	}
	const sx = rect.width > 0 ? size / rect.width : 1;
	const sy = rect.height > 0 ? size / rect.height : 1;
	const px = (clientX - rect.left) * sx;
	const py = (clientY - rect.top) * sy;
	return radarValueAtPointer(px, py, C, ang(i), R, view.hexMax);
}

onMount(() => {
	const onMove = (e: PointerEvent): void => {
		if (dragIndex === null) {
			return;
		}
		if (!dragMoved) {
			// Stay a tap until the pointer leaves the dead-zone, so a jittery tap
			// keeps its +1 click instead of being mistaken for a (no-op) drag.
			const dx = e.clientX - dragStartX;
			const dy = e.clientY - dragStartY;
			if (dx * dx + dy * dy < DRAG_THRESHOLD_PX * DRAG_THRESHOLD_PX) {
				return;
			}
			dragMoved = true;
		}
		e.preventDefault();
		view.setValue(dragIndex, valueAtClient(e.clientX, e.clientY, dragIndex));
	};
	const onEnd = (): void => {
		if (dragIndex === null) {
			return;
		}
		dragIndex = null;
		// Release the pinned scale so it recomputes to fit the final allocation.
		view.unlockScale();
	};
	window.addEventListener('pointermove', onMove);
	window.addEventListener('pointerup', onEnd);
	window.addEventListener('pointercancel', onEnd);
	return () => {
		window.removeEventListener('pointermove', onMove);
		window.removeEventListener('pointerup', onEnd);
		window.removeEventListener('pointercancel', onEnd);
	};
});
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

	&.interactive {
		cursor: grab;
		// Keep a touch press-drag driving the gesture instead of scrolling the page.
		touch-action: none;
	}

	&.dragging {
		cursor: grabbing;
	}

	&:focus-visible .dot,
	&.dragging .dot {
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
