<!-- The Synthesis "Web" view canvas (spike #1125 area E): the discovered recipe DAG — input skills →
     fusion nodes → result skills, with a synthesized result feeding a deeper recipe drawn as an onward edge
     (recipe-graph chaining, the "depth source"). The layout/edge math is the framework-free
     `layoutSynthesisGraph`; this component only paints the SVG edges + positioned nodes and wires pointer
     pan (drag) and wheel zoom through the pure `zoomAt` transform. Selecting a fusion/result node drives the
     shared result dossier via `onSelect`, exactly as the Bench list does. -->
<div
	class="canvas"
	data-testid="synthesis-graph"
	bind:this={container}
	onpointerdown={onPointerDown}
	onpointermove={onPointerMove}
	onpointerup={endDrag}
	onpointerleave={endDrag}
	onwheel={onWheel}
	role="application"
	aria-label="Recipe graph — drag to pan, scroll to zoom"
>
	<div class="viewport" style:transform="translate({vp.x}px, {vp.y}px) scale({vp.scale})">
		<svg class="edges" width={layout.width} height={layout.height} aria-hidden="true">
			{#each lines as line (line.key)}
				<line
					x1={line.x1}
					y1={line.y1}
					x2={line.x2}
					y2={line.y2}
					stroke={line.color}
					stroke-width="1.6"
					stroke-dasharray={line.dashed ? '5 5' : undefined}
				/>
			{/each}
		</svg>
		{#each layout.nodes as node (node.key)}
			<SynthesisGraphNode {node} selected={node.selectable && node.recipeId === selectedRecipeId} {onSelect} />
		{/each}
	</div>

	<div class="legend">
		<span>◆ fusion · ▢ input · ⬡ result</span>
		<span>solid = ready · ·· = hint / gate</span>
	</div>
	<button type="button" class="reset" onclick={recenter}>⊹ reset view</button>
</div>

<script lang="ts">
import { recipeStateAccent } from './synthesis';
import { type SynthesisGraphLayout, type Viewport, zoomAt } from './synthesis-graph';
import SynthesisGraphNode from './SynthesisGraphNode.svelte';

type Props = {
	layout: SynthesisGraphLayout;
	selectedRecipeId: number | null;
	onSelect: (recipeId?: number) => void;
};

const { layout, selectedRecipeId, onSelect }: Props = $props();

let container = $state<HTMLDivElement>();
let vp = $state<Viewport>({ x: 0, y: 0, scale: 1 });

// Drag-to-pan state. The pointer is captured so a drag that leaves the node still pans the canvas.
let dragging = false;
let lastX = 0;
let lastY = 0;

const nodeByKey = $derived(new Map(layout.nodes.map((n) => [n.key, n])));

/** The edges as drawable segments between node centres, styled by the feeding recipe's state (dashed +
 *  muted for an unsettled hint/gate, solid + accent/success for ready/done). */
const lines = $derived(
	layout.edges.flatMap((edge) => {
		const from = nodeByKey.get(edge.from);
		const to = nodeByKey.get(edge.to);
		if (!from || !to) {
			return [];
		}
		return [
			{
				key: edge.key,
				x1: from.x,
				y1: from.y,
				x2: to.x,
				y2: to.y,
				color: recipeStateAccent(edge.state),
				dashed: edge.state === 'hinted' || edge.state === 'gated'
			}
		];
	})
);

/** Centre the content in the container at scale 1 (the initial view + the reset action). */
function recenter(): void {
	if (!container || !layout.width) {
		return;
	}
	vp = {
		scale: 1,
		x: (container.clientWidth - layout.width) / 2,
		y: (container.clientHeight - layout.height) / 2
	};
}

function onPointerDown(event: PointerEvent): void {
	dragging = true;
	lastX = event.clientX;
	lastY = event.clientY;
	container?.setPointerCapture(event.pointerId);
}

function onPointerMove(event: PointerEvent): void {
	if (!dragging) {
		return;
	}
	vp = { ...vp, x: vp.x + (event.clientX - lastX), y: vp.y + (event.clientY - lastY) };
	lastX = event.clientX;
	lastY = event.clientY;
}

function endDrag(event: PointerEvent): void {
	dragging = false;
	if (container?.hasPointerCapture(event.pointerId)) {
		container.releasePointerCapture(event.pointerId);
	}
}

function onWheel(event: WheelEvent): void {
	event.preventDefault();
	const rect = container?.getBoundingClientRect();
	if (!rect) {
		return;
	}
	// Zoom toward the cursor: a downward (positive) wheel zooms out, upward zooms in.
	const factor = event.deltaY < 0 ? 1.1 : 1 / 1.1;
	vp = zoomAt(vp, factor, event.clientX - rect.left, event.clientY - rect.top);
}

// Recentre on mount (this runs once after the initial render, when `container` is bound) and whenever the
// graph's content extent changes (recipe set / view switched). Reads only the layout dims + container, so
// writing `vp` here can't loop.
$effect(() => {
	void layout.width;
	void layout.height;
	recenter();
});
</script>

<style lang="scss">
.canvas {
	position: relative;
	overflow: hidden;
	border: 1px solid var(--border-subtle);
	border-radius: 6px;
	background:
		radial-gradient(circle at 1px 1px, color-mix(in srgb, var(--white) 5%, transparent) 1px, transparent 0) 0 0 / 24px
			24px,
		var(--surface);
	cursor: grab;
	touch-action: none;

	&:active {
		cursor: grabbing;
	}
}

.viewport {
	position: absolute;
	top: 0;
	left: 0;
	transform-origin: 0 0;
}

.edges {
	position: absolute;
	top: 0;
	left: 0;
	overflow: visible;
	pointer-events: none;
}

.legend {
	position: absolute;
	left: 12px;
	bottom: 10px;
	display: flex;
	flex-direction: column;
	gap: 2px;
	padding: 6px 9px;
	border: 1px solid var(--border-light);
	border-radius: 4px;
	background: color-mix(in srgb, var(--surface) 90%, transparent);
	font-family: var(--mono);
	font-size: 8.5px;
	line-height: 1.5;
	color: var(--text-muted);
	pointer-events: none;
}

.reset {
	position: absolute;
	right: 12px;
	top: 10px;
	padding: 5px 10px;
	border: 1px solid var(--border-light);
	border-radius: 4px;
	background: color-mix(in srgb, var(--surface) 90%, transparent);
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 0.4px;
	color: var(--text-tertiary);
	cursor: pointer;

	&:hover {
		color: var(--text-primary);
		border-color: color-mix(in srgb, var(--accent) 40%, var(--border-light));
	}
}
</style>
