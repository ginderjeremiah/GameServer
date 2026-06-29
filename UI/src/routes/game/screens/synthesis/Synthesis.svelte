<!-- Synthesis screen (spike #1125 area E).

     The player-driven forge: combine owned skills into new ones. Recipes are revealed by the spike's
     conservative hinted reveal — a recipe is fully shown once every input is owned, a partly-owned recipe
     is a vague hint, and a recipe with no owned input is hidden. The list (left) drives the bench (centre,
     the inputs→result act + the wired Synthesize action) and the result dossier (right). Proficiency
     progress is re-fetched on mount (mirroring the Proficiencies screen) so the gate state is current;
     the recipe/skill/proficiency reference data is already in `staticData`. -->
<div class="synth-frame" data-testid="synthesis-screen">
	<div class="header">
		<span class="diamond"></span>
		<div>
			<div class="eyebrow">Player-driven · combine owned skills into new ones</div>
			<h1 class="title">Synthesis</h1>
		</div>
		<div class="spacer"></div>
		<div class="discovered">
			<span class="k">discovered</span>
			<span class="v">{view.discoveredCount}</span>
		</div>
	</div>

	<div class="body">
		{#if view.error}
			<div class="state" data-testid="synthesis-error">
				<div class="state-title">Couldn’t load synthesis</div>
				<p class="state-copy">Something went wrong loading your synthesis data. Please try again later.</p>
			</div>
		{:else if !view.loading && view.isEmpty}
			<div class="state" data-testid="synthesis-empty">
				<div class="state-title">No formulae discovered yet</div>
				<p class="state-copy">
					Acquire a skill that feeds a recipe and it will appear here — gather both inputs to reveal the formula, then
					synthesize a new skill.
				</p>
			</div>
		{:else if !view.loading}
			<div class="toolbar">
				{#if view.mode === 'bench'}
					<div class="filters">
						{#each filterChips as chip (chip.key)}
							<button
								type="button"
								class="chip"
								class:active={view.filter === chip.key}
								onclick={() => view.setFilter(chip.key)}
							>
								{chip.label} · {chip.count}
							</button>
						{/each}
					</div>
				{:else}
					<div class="filters"></div>
				{/if}

				<div class="view-toggle" role="tablist" aria-label="Synthesis view">
					<button
						type="button"
						role="tab"
						class="tab"
						class:active={view.mode === 'bench'}
						aria-selected={view.mode === 'bench'}
						data-testid="view-bench"
						onclick={() => view.setMode('bench')}>▤ Bench</button
					>
					<button
						type="button"
						role="tab"
						class="tab"
						class:active={view.mode === 'web'}
						aria-selected={view.mode === 'web'}
						data-testid="view-web"
						onclick={() => view.setMode('web')}>⌗ Web</button
					>
				</div>
			</div>

			{#if view.mode === 'bench'}
				<div class="cols">
					<div class="list sx-scroll" data-testid="synthesis-list">
						{#if view.visibleRecipes.length === 0}
							<div class="list-empty">No recipes in this filter.</div>
						{:else}
							{#each view.visibleRecipes as recipe (recipe.id)}
								<RecipeRow {recipe} selected={recipe.id === view.selected?.id} onSelect={(id) => view.select(id)} />
							{/each}
						{/if}
					</div>

					<SynthesisBench
						recipe={view.selected}
						synthesizing={view.synthesizing}
						onSynthesize={confirmAndSynthesize}
						onViewInSkills={viewInSkills}
					/>

					<ResultDossier recipe={view.selected} />
				</div>
			{:else}
				<div class="web-cols">
					<SynthesisGraph layout={view.graph} selectedRecipeId={view.selected?.id ?? null} onSelect={selectFromGraph} />
					<ResultDossier recipe={view.selected} />
				</div>
			{/if}
		{/if}
	</div>

	{#if view.loading}
		<Loading delay={100} />
	{/if}
</div>

<script lang="ts">
import { onMount } from 'svelte';
import { Loading } from '$components';
import { confirmModal, navigation, playerProficiencies, toastError } from '$stores';
import RecipeRow from './RecipeRow.svelte';
import SynthesisBench from './SynthesisBench.svelte';
import ResultDossier from './ResultDossier.svelte';
import SynthesisGraph from './SynthesisGraph.svelte';
import { RECIPE_STATE_LABEL, type RecipeView } from './synthesis';
import { type RecipeFilter, SynthesisView } from './synthesis-view.svelte';

const view = new SynthesisView();

const filterChips = $derived<{ key: RecipeFilter; label: string; count: number }[]>([
	{ key: 'all', label: 'All', count: view.discoveredCount },
	{ key: 'ready', label: RECIPE_STATE_LABEL.ready, count: view.counts.ready },
	{ key: 'gated', label: RECIPE_STATE_LABEL.gated, count: view.counts.gated },
	{ key: 'hinted', label: RECIPE_STATE_LABEL.hinted, count: view.counts.hinted },
	{ key: 'done', label: RECIPE_STATE_LABEL.done, count: view.counts.done }
]);

// Synthesis is non-consumptive and one-time, so a confirm dialog (rather than an undo) is the right
// guard before forging — it also restates that inputs are kept.
async function confirmAndSynthesize(recipe: RecipeView): Promise<void> {
	const inputs = recipe.inputs
		.map((i) => i.skill?.name ?? '')
		.filter(Boolean)
		.join(' + ');
	const confirmed = await confirmModal({
		title: `Synthesize ${recipe.result?.name ?? 'this skill'}?`,
		body: `${inputs} → ${recipe.result?.name ?? 'a new skill'}. Inputs are kept — nothing is consumed. The new skill joins your skills, unselected.`,
		confirmLabel: 'Synthesize'
	});
	if (confirmed) {
		await view.synthesize(recipe);
	}
}

function viewInSkills(): void {
	navigation.requestScreen('skills');
}

// A graph node carries the recipe it belongs to (fusion + result); a leaf input node has none, so a click
// on it is a no-op. Selecting drives the shared dossier exactly as the bench list does.
function selectFromGraph(recipeId?: number): void {
	if (recipeId !== undefined) {
		view.select(recipeId);
	}
}

onMount(async () => {
	// Force a fresh fetch so the gate state reflects proficiency progress since the store was last loaded.
	await playerProficiencies.load(true);
	view.error = playerProficiencies.error;
	if (playerProficiencies.error) {
		toastError('Your synthesis data could not be loaded. Please try again later.');
	}
	view.loading = false;
});
</script>

<style lang="scss">
.synth-frame {
	position: relative;
	height: 100%;
	display: flex;
	flex-direction: column;
	color: var(--text-primary);
	font-family: var(--sans);
	overflow: hidden;
}

.header {
	display: flex;
	align-items: center;
	gap: 13px;
	padding: 18px 24px 14px;
	flex-shrink: 0;
	border-bottom: 1px solid var(--border-subtle);

	.spacer {
		flex: 1;
	}
}

.diamond {
	width: 11px;
	height: 11px;
	flex-shrink: 0;
	transform: rotate(45deg);
	background: var(--accent);
	box-shadow: 0 0 8px color-mix(in srgb, var(--accent) 60%, transparent);
}

.eyebrow {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--eyebrow);
}

.title {
	margin: 0;
	font-size: 22px;
	font-weight: 500;
	letter-spacing: -0.3px;
	line-height: 1;
}

.discovered {
	display: flex;
	align-items: baseline;
	gap: 8px;

	.k {
		font-family: var(--mono);
		font-size: 9px;
		letter-spacing: 1.4px;
		text-transform: uppercase;
		color: var(--text-muted);
	}

	.v {
		font-family: var(--mono);
		font-size: 15px;
		color: var(--text-primary);
	}
}

.body {
	flex: 1;
	min-height: 0;
	display: flex;
	flex-direction: column;
	padding: 14px 24px 22px;
}

.toolbar {
	flex-shrink: 0;
	display: flex;
	align-items: flex-start;
	gap: 12px;
	margin-bottom: 14px;
}

.filters {
	flex: 1;
	display: flex;
	gap: 7px;
	flex-wrap: wrap;
}

.view-toggle {
	flex-shrink: 0;
	display: flex;
	gap: 4px;
	padding: 3px;
	border: 1px solid var(--border-light);
	border-radius: 6px;
}

.tab {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 0.4px;
	padding: 4px 12px;
	border: none;
	border-radius: 4px;
	background: transparent;
	color: var(--text-tertiary);
	cursor: pointer;

	&.active {
		background: var(--accent);
		color: var(--text-on-accent);
	}
}

.chip {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 0.4px;
	padding: 4px 12px;
	border-radius: 20px;
	border: 1px solid var(--border-light);
	background: transparent;
	color: var(--text-tertiary);
	cursor: pointer;

	&.active {
		border-color: var(--accent);
		background: var(--accent);
		color: var(--text-on-accent);
	}
}

.cols {
	flex: 1;
	min-height: 0;
	display: grid;
	grid-template-columns: 300px 1fr 320px;
	gap: 16px;
}

.web-cols {
	flex: 1;
	min-height: 0;
	display: grid;
	grid-template-columns: 1fr 320px;
	gap: 16px;
}

.list {
	overflow-y: auto;
	padding-right: 4px;
}

.list-empty {
	font-size: 12.5px;
	color: var(--text-muted);
	padding: 12px 4px;
}

.state {
	flex: 1;
	min-height: 0;
	display: flex;
	flex-direction: column;
	align-items: center;
	justify-content: center;
	text-align: center;
	gap: 10px;
	padding: 24px;
}

.state-title {
	font-size: 18px;
	font-weight: 500;
}

.state-copy {
	margin: 0;
	max-width: 440px;
	font-size: 13px;
	line-height: 1.6;
	color: var(--text-tertiary);
}

@media (max-width: 1000px) {
	.cols {
		grid-template-columns: 260px 1fr;
	}

	.web-cols {
		grid-template-columns: 1fr 260px;
	}
}
</style>
