<!-- One node in the Synthesis "Web" graph. Three kinds (spike #1125 area E): a leaf `input` skill
     (rarity-bordered, non-interactive), a recipe `fusion` (a diamond — filled when ready/done, sealed-dashed
     when hinted/gated), and a `result` skill (a card; masked to a sealed glyph while the recipe is hinted so
     the identity never leaks, revealed with its name + rarity otherwise). Fusion/result select the recipe. -->
{#if node.kind === 'fusion'}
	<button
		type="button"
		class="node fusion"
		class:selected
		class:sealed={node.masked}
		style:left="{box.left}px"
		style:top="{box.top}px"
		style:width="{size.w}px"
		style:height="{size.h}px"
		style:--node-accent={accent}
		aria-label={selectLabel}
		aria-pressed={selected}
		data-testid="graph-node-fusion-{node.recipeId}"
		onclick={() => onSelect(node.recipeId)}
	></button>
{:else if node.kind === 'input'}
	<div
		class="node input"
		style:left="{box.left}px"
		style:top="{box.top}px"
		style:width="{size.w}px"
		style:height="{size.h}px"
		style:--node-rarity={rarity}
		title={node.skill?.name}
	>
		<span class="name">{node.skill?.name ?? ''}</span>
		<span class="kicker">{node.skill ? rarityLabel(node.skill.rarityId) : 'input'}</span>
	</div>
{:else}
	<button
		type="button"
		class="node result"
		class:selected
		class:sealed={node.masked}
		style:left="{box.left}px"
		style:top="{box.top}px"
		style:width="{size.w}px"
		style:height="{size.h}px"
		style:--node-rarity={rarity}
		style:--node-accent={accent}
		aria-label={selectLabel}
		aria-pressed={selected}
		data-testid="graph-node-result-{node.recipeId}"
		onclick={() => onSelect(node.recipeId)}
	>
		<span class="bar">{node.skill ? rarityLabel(node.skill.rarityId) : 'sealed'}</span>
		{#if node.masked}
			<span class="glyph" aria-hidden="true">⟁</span>
		{:else}
			<span class="name">{node.skill?.name ?? ''}</span>
		{/if}
		<span class="tag">{stateLabel}</span>
	</button>
{/if}

<script lang="ts">
import { rarityColor, rarityLabel } from '$lib/common';
import { RECIPE_STATE_LABEL, recipeStateAccent } from './synthesis';
import { NODE_SIZE, type GraphNode } from './synthesis-graph';

type Props = {
	node: GraphNode;
	selected: boolean;
	onSelect: (recipeId?: number) => void;
};

const { node, selected, onSelect }: Props = $props();

const size = $derived(NODE_SIZE[node.kind]);
/** Top-left from the node's stored centre — the single positioning source shared with the edge math. */
const box = $derived({ left: node.x - size.w / 2, top: node.y - size.h / 2 });

const accent = $derived(node.state ? recipeStateAccent(node.state) : 'var(--accent)');
const rarity = $derived(node.skill ? rarityColor(node.skill.rarityId) : 'var(--border-light)');
const stateLabel = $derived(node.state ? RECIPE_STATE_LABEL[node.state] : '');
const selectLabel = $derived(node.masked ? 'Undiscovered formula' : (node.skill?.name ?? 'Recipe'));
</script>

<style lang="scss">
.node {
	position: absolute;
	box-sizing: border-box;
	font-family: var(--sans);
	color: var(--text-primary);
}

.input {
	display: flex;
	flex-direction: column;
	justify-content: center;
	gap: 2px;
	padding: 5px 9px;
	border: 1px solid color-mix(in srgb, var(--node-rarity) 28%, transparent);
	border-left: 3px solid var(--node-rarity);
	border-radius: 3px;
	background: color-mix(in srgb, var(--node-rarity) 9%, var(--surface));
	text-align: left;

	.name {
		font-size: 11.5px;
		font-weight: 500;
		line-height: 1.1;
		white-space: nowrap;
		overflow: hidden;
		text-overflow: ellipsis;
	}

	.kicker {
		font-family: var(--mono);
		font-size: 6.5px;
		letter-spacing: 0.8px;
		text-transform: uppercase;
		color: var(--node-rarity);
	}
}

.fusion {
	padding: 0;
	transform: rotate(45deg);
	border: 1.5px solid var(--node-accent);
	border-radius: 4px;
	background: var(--node-accent);
	box-shadow: 0 0 10px color-mix(in srgb, var(--node-accent) 45%, transparent);
	cursor: pointer;

	&.sealed {
		border-style: dashed;
		background: var(--surface);
		box-shadow: none;
	}

	&.selected {
		box-shadow: 0 0 0 2px var(--node-accent);
	}
}

.result {
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	gap: 3px;
	padding: 0 0 6px;
	overflow: hidden;
	border: 2px solid var(--node-rarity);
	border-radius: 6px;
	background: var(--surface);
	cursor: pointer;
	box-shadow: 0 0 12px color-mix(in srgb, var(--node-rarity) 17%, transparent);

	&.sealed {
		border-style: dashed;
		border-color: color-mix(in srgb, var(--node-accent) 55%, var(--border-light));
	}

	&.selected {
		background: color-mix(in srgb, var(--node-rarity) 12%, var(--surface));
		box-shadow:
			0 0 0 1px var(--node-rarity),
			0 0 16px color-mix(in srgb, var(--node-rarity) 33%, transparent);
	}

	.bar {
		width: 100%;
		font-family: var(--mono);
		font-size: 7px;
		font-weight: 600;
		letter-spacing: 1px;
		text-transform: uppercase;
		text-align: center;
		padding: 2px 0;
		color: var(--text-on-accent);
		background: var(--node-rarity);
	}

	&.sealed .bar {
		color: var(--node-accent);
		background: transparent;
	}

	.glyph {
		font-size: 22px;
		line-height: 1;
		color: var(--node-accent);
		margin-top: 4px;
	}

	.name {
		font-size: 12.5px;
		font-weight: 600;
		line-height: 1.05;
		text-align: center;
		padding: 0 6px;
		white-space: nowrap;
		overflow: hidden;
		text-overflow: ellipsis;
		max-width: 100%;
		margin-top: 4px;
	}

	.tag {
		font-family: var(--mono);
		font-size: 7px;
		letter-spacing: 0.6px;
		text-transform: uppercase;
		color: var(--node-accent);
	}
}
</style>
