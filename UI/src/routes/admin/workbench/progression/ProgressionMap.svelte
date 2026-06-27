<div class="map" bind:this={container} data-testid="progression-map">
	<svg class="edges" aria-hidden="true" style="width:{svgW}px;height:{svgH}px">
		<defs>
			<marker id="prog-gateway-arrow" markerWidth="8" markerHeight="8" refX="6" refY="3" orient="auto">
				<path d="M0,0 L6,3 L0,6 Z" fill="var(--accent)" />
			</marker>
		</defs>
		{#each edges as d, i (i)}
			<path
				class="edge"
				{d}
				fill="none"
				stroke="var(--accent)"
				stroke-width="1.5"
				stroke-dasharray="5 4"
				marker-end="url(#prog-gateway-arrow)"
			/>
		{/each}
	</svg>

	<div class="columns">
		{#each columns as col (col.id)}
			<div class="col" class:retired={col.retired}>
				<button type="button" class="col-head" class:gated={col.gatedPath} onclick={() => open(col.id)}>
					{col.name || 'Unnamed path'}
				</button>
				{#each col.nodes as node, i (node.id)}
					{#if i > 0}
						<div class="connector"></div>
					{/if}
					<button
						type="button"
						class="node"
						class:gated={node.gated}
						class:retired={node.retired}
						data-node={node.nodeId}
						onclick={() => drill(col.id, node.id)}
					>
						<span class="node-ord">T{node.ordinal}{node.gated ? ' · gated ✦' : ''}</span>
						<span class="node-name">{node.name || 'Unnamed tier'}</span>
						<span class="node-cap">cap {node.maxLevel}</span>
					</button>
				{/each}
				{#if col.nodes.length === 0}
					<div class="col-empty">No tiers</div>
				{/if}
			</div>
		{/each}
		{#if columns.length === 0}
			<div class="map-empty">No paths to map</div>
		{/if}
	</div>

	<div class="legend">solid = within-path order · ┄► dashed = gateway prerequisite (maxed) opens a gated path</div>
</div>

<script lang="ts">
import { onMount, tick } from 'svelte';
import type { ProgressionStore } from './progression-store.svelte';
import { bezierPath, mapColumns, mapEdgeDefs } from './progression-map';

interface Props {
	store: ProgressionStore;
	/** Switch the surface back to the List editor after a node/header navigates. */
	onNavigate: () => void;
}

const { store, onNavigate }: Props = $props();

let container = $state<HTMLDivElement>();
let edges = $state<string[]>([]);
let svgW = $state(0);
let svgH = $state(0);

const columns = $derived(mapColumns(store.paths, store.profs));
const edgeDefs = $derived(mapEdgeDefs(store.profs));

const open = (pathId: number) => {
	store.selectPath(pathId);
	onNavigate();
};

const drill = (pathId: number, tierId: number) => {
	store.selectPath(pathId);
	store.drillTier(tierId);
	onNavigate();
};

/** Measure each gateway edge from the right edge of its prerequisite node to the left edge of the gated node. */
const measureEdges = () => {
	if (!container) {
		return;
	}
	const crect = container.getBoundingClientRect();
	const paths: string[] = [];
	for (const edge of edgeDefs) {
		const from = container.querySelector(`[data-node="${edge.from}"]`);
		const to = container.querySelector(`[data-node="${edge.to}"]`);
		if (!(from instanceof HTMLElement) || !(to instanceof HTMLElement)) {
			continue;
		}
		const fr = from.getBoundingClientRect();
		const tr = to.getBoundingClientRect();
		const x1 = fr.right - crect.left + container.scrollLeft;
		const y1 = fr.top + fr.height / 2 - crect.top + container.scrollTop;
		const x2 = tr.left - crect.left + container.scrollLeft;
		const y2 = tr.top + tr.height / 2 - crect.top + container.scrollTop;
		paths.push(bezierPath(x1, y1, x2, y2));
	}
	edges = paths;
	svgW = container.scrollWidth;
	svgH = container.scrollHeight;
};

// Re-measure after the columns/edges (and so the laid-out DOM) change.
$effect(() => {
	void columns;
	void edgeDefs;
	void tick().then(measureEdges);
});

onMount(() => {
	if (!container) {
		return;
	}
	const observer = new ResizeObserver(() => measureEdges());
	observer.observe(container);
	return () => observer.disconnect();
});
</script>

<style lang="scss">
.map {
	flex: 1;
	min-height: 0;
	overflow: auto;
	position: relative;
	padding: 30px 40px;
	background: var(--page);
}
.edges {
	position: absolute;
	top: 0;
	left: 0;
	pointer-events: none;
	overflow: visible;
}
.edge {
	opacity: 0.85;
}
.columns {
	position: relative;
	display: flex;
	gap: 52px;
	align-items: flex-start;
	min-width: max-content;
	padding-bottom: 10px;
}
.col {
	display: flex;
	flex-direction: column;
	align-items: center;

	&.retired {
		opacity: 0.55;
	}
}
.col-head {
	background: transparent;
	border: none;
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--text-tertiary);
	margin-bottom: 16px;
	cursor: pointer;
	white-space: nowrap;
	padding: 0;

	&.gated {
		color: var(--accent);
	}
	&:hover {
		color: var(--accent-light);
	}
}
.connector {
	width: 2px;
	height: 18px;
	background: var(--border-light);
}
.node {
	width: 132px;
	border: 1px solid var(--border-light);
	border-radius: 6px;
	padding: 8px 10px;
	background: var(--panel);
	text-align: center;
	cursor: pointer;
	display: flex;
	flex-direction: column;
	gap: 2px;
	transition: border-color 0.14s ease;

	&.gated {
		border: 1.5px dashed var(--accent);
		background: color-mix(in srgb, var(--accent) 7%, transparent);
	}
	&.retired {
		opacity: 0.7;
	}
	&:hover {
		border-color: var(--accent);
	}
}
.node-ord {
	font-family: var(--mono);
	font-size: 8.5px;
	color: var(--text-tertiary);
}
.node-name {
	font-size: 12.5px;
	font-weight: 500;
	color: var(--text-primary);
}
.node-cap {
	font-family: var(--mono);
	font-size: 8.5px;
	color: var(--text-muted);
}
.col-empty {
	font-family: var(--mono);
	font-size: 10px;
	color: var(--text-muted);
}
.map-empty {
	color: var(--text-muted);
	font-size: 12.5px;
}
.legend {
	position: relative;
	margin-top: 26px;
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-muted);
}
</style>
