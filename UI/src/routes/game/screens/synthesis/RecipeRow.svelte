<!-- One recipe in the Synthesis list. A revealed recipe (ready/gated/done) shows its result skill's
     name, rarity and inputs; a hinted recipe keeps the result and its missing inputs masked — only the
     owned input(s) and a vague "Undiscovered pairing" surface, so discovery stays "gather the inputs". -->
<button
	type="button"
	class="row"
	class:selected
	style:--state-accent={accent}
	style:--row-rarity={rarityBorder}
	onclick={() => onSelect(recipe.id)}
	aria-pressed={selected}
	data-testid="recipe-{recipe.id}"
>
	<span class="dot" class:hollow={recipe.state === 'hinted'} aria-hidden="true"></span>
	<span class="body">
		<span class="name" class:sealed={!recipe.result}>{title}</span>
		<span class="inputs">{inputSummary}</span>
	</span>
	<span class="meta">
		{#if recipe.result}
			<span class="rarity">{rarityLabel(recipe.result.rarityId)}</span>
		{/if}
		<span class="tag">{stateLabel}</span>
	</span>
</button>

<script lang="ts">
import { rarityColor, rarityLabel } from '$lib/common';
import { RECIPE_STATE_LABEL, recipeStateAccent, type RecipeView } from './synthesis';

type Props = {
	recipe: RecipeView;
	selected: boolean;
	onSelect: (id: number) => void;
};

const { recipe, selected, onSelect }: Props = $props();

const accent = $derived(recipeStateAccent(recipe.state));
const stateLabel = $derived(RECIPE_STATE_LABEL[recipe.state]);
const rarityBorder = $derived(recipe.result ? rarityColor(recipe.result.rarityId) : 'var(--border-light)');

/** The row title: the result skill's name once revealed, else a sealed placeholder. */
const title = $derived(recipe.result?.name ?? 'Undiscovered pairing');

/** A masked-aware "A + B" input summary: owned inputs by name, unowned masked as "▒▒▒". */
const inputSummary = $derived(recipe.inputs.map((input) => input.skill?.name ?? '▒▒▒').join(' + '));
</script>

<style lang="scss">
.row {
	display: flex;
	align-items: center;
	gap: 10px;
	width: 100%;
	padding: 9px 11px;
	margin-bottom: 6px;
	border: 1px solid var(--border-subtle);
	border-left: 3px solid var(--row-rarity);
	border-radius: 4px;
	background: color-mix(in srgb, var(--white) 2%, transparent);
	color: var(--text-primary);
	font-family: var(--sans);
	text-align: left;
	cursor: pointer;
	transition:
		border-color 0.12s,
		background 0.12s;

	&:hover {
		border-color: color-mix(in srgb, var(--accent) 35%, var(--border-subtle));
	}

	&.selected {
		border-color: color-mix(in srgb, var(--accent) 55%, transparent);
		background: color-mix(in srgb, var(--accent) 10%, transparent);
	}
}

.dot {
	flex-shrink: 0;
	width: 11px;
	height: 11px;
	transform: rotate(45deg);
	background: var(--state-accent);
	box-shadow: 0 0 7px color-mix(in srgb, var(--state-accent) 55%, transparent);

	&.hollow {
		background: transparent;
		border: 1.5px dashed var(--state-accent);
		box-shadow: none;
		border-radius: 50%;
	}
}

.body {
	flex: 1;
	min-width: 0;
	display: flex;
	flex-direction: column;
	gap: 3px;
}

.name {
	font-size: 14px;
	font-weight: 500;
	line-height: 1.1;
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;

	&.sealed {
		color: var(--text-tertiary);
		font-style: italic;
	}
}

.inputs {
	font-family: var(--mono);
	font-size: 9.5px;
	color: var(--text-muted);
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
}

.meta {
	flex-shrink: 0;
	display: flex;
	flex-direction: column;
	align-items: flex-end;
	gap: 5px;
}

.rarity {
	font-family: var(--mono);
	font-size: 7.5px;
	font-weight: 600;
	letter-spacing: 1px;
	text-transform: uppercase;
	padding: 1px 7px;
	border-radius: 10px;
	color: var(--row-rarity);
	border: 1px solid color-mix(in srgb, var(--row-rarity) 45%, transparent);
	background: color-mix(in srgb, var(--row-rarity) 12%, transparent);
}

.tag {
	font-family: var(--mono);
	font-size: 8px;
	letter-spacing: 1px;
	text-transform: uppercase;
	color: var(--state-accent);
}
</style>
