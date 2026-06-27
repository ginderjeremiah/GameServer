<div class="map" data-testid="progression-map">
	<div class="columns">
		{#each columns as col (col.id)}
			<div class="col" class:retired={col.retired}>
				<button type="button" class="col-head" onclick={() => open(col.id)}>
					{col.name || 'Unnamed path'}
				</button>
				{#each col.nodes as node, i (node.id)}
					{#if i > 0}
						<div class="connector"></div>
					{/if}
					<button
						type="button"
						class="node"
						class:retired={node.retired}
						data-node={node.nodeId}
						onclick={() => drill(col.id, node.id)}
					>
						<span class="node-ord">T{node.ordinal}</span>
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

	<div class="legend">solid = within-path order (a tier opens once the tier before it is maxed)</div>
</div>

<script lang="ts">
import type { ProgressionStore } from './progression-store.svelte';
import { mapColumns } from './progression-map';

interface Props {
	store: ProgressionStore;
	/** Switch the surface back to the List editor after a node/header navigates. */
	onNavigate: () => void;
}

const { store, onNavigate }: Props = $props();

const columns = $derived(mapColumns(store.paths, store.profs));

const open = (pathId: number) => {
	store.selectPath(pathId);
	onNavigate();
};

const drill = (pathId: number, tierId: number) => {
	store.selectPath(pathId);
	store.drillTier(tierId);
	onNavigate();
};
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
