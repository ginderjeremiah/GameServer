<!-- Proficiencies screen — "The Lexicon".

     A minimal scaffold over the {@link ProficienciesView} logic core: a rail of discovered paths and the
     selected path's spine of tiers. The rich visuals (illuminated spine + pips, the word-detail inspector)
     land in the rail/spine (#1217) and inspector (#1218) sub-issues — this proves the view-model end-to-end
     and registers the screen in the nav.

     Progress is re-fetched on mount (mirroring the Statistics screen) so the lexicon reflects play since the
     store was last loaded at game boot; the reference data is already in `staticData`. -->
<div class="prof-frame" data-testid="proficiencies-screen">
	<div class="header">
		<div class="eyebrow">Character · Proficiencies</div>
		<div class="title-line">
			<h1 class="title">The Lexicon</h1>
			<span class="sub">paths of mastery, deciphered as you train</span>
		</div>
	</div>

	<div class="body">
		{#if view.error}
			<div class="state" data-testid="proficiencies-error">
				<div class="state-title">Couldn’t load proficiencies</div>
				<p class="state-copy">Something went wrong fetching your proficiencies. Please try again later.</p>
			</div>
		{:else if !view.loading && view.isEmpty}
			<div class="state" data-testid="proficiencies-empty">
				<div class="state-title">No words of power yet</div>
				<p class="state-copy">
					Acquire a skill that trains a proficiency and its path will appear here, ready to decipher.
				</p>
			</div>
		{:else if view.selectedPath}
			<div class="cols">
				<!-- rail: discovered paths -->
				<nav class="rail" aria-label="Lexicon paths">
					{#each view.paths as path (path.id)}
						<button
							type="button"
							class="rail-row"
							class:active={path.id === view.selectedPath?.id}
							onclick={() => view.selectPath(path.id)}
						>
							<WordOfPower text={path.word} label={path.name} size={18} />
							<span class="rail-name">{path.name}</span>
						</button>
					{/each}
				</nav>

				<!-- spine: the selected path's tiers, most-advanced first -->
				<div class="spine">
					{#each [...view.selectedPath.tiers].reverse() as tier (tier.id)}
						<button
							type="button"
							class="tier"
							class:active={tier.id === view.selectedTier?.id}
							onclick={() => view.selectTier(tier.id)}
						>
							<WordOfPower text={tier.word} label={tier.name} size={26} glow={tier.decipher === 'translated'} />
							<div class="tier-meta">
								<span class="tier-name">{tier.name}</span>
								<span class="tier-tags">
									<span class="tag state-{tier.state}">{tier.state}</span>
									<span class="tag">Lv {tier.level}/{tier.maxLevel}</span>
									<span class="tag">{tier.decipher}</span>
								</span>
							</div>
						</button>
					{/each}
				</div>
			</div>
		{/if}
	</div>

	{#if view.loading}
		<Loading delay={100} />
	{/if}
</div>

<script lang="ts">
import { onMount } from 'svelte';
import { Loading, WordOfPower } from '$components';
import { playerProficiencies, toastError } from '$stores';
import { ProficienciesView } from './proficiencies-view.svelte';

const view = new ProficienciesView();

onMount(async () => {
	// Force a fresh fetch so progress reflects play since the store was last loaded at game boot.
	await playerProficiencies.load(true);
	view.error = playerProficiencies.error;
	if (playerProficiencies.error) {
		toastError('Your proficiencies could not be loaded. Please try again later.');
	}
	view.loading = false;
});
</script>

<style lang="scss">
.prof-frame {
	height: 100%;
	display: flex;
	flex-direction: column;
	position: relative;
	color: var(--text-primary);
	font-family: var(--sans);
	overflow: hidden;
}

.header {
	padding: 20px 28px 0;
	flex-shrink: 0;
}

.eyebrow {
	font-family: var(--mono);
	font-size: 9.5px;
	letter-spacing: 2px;
	text-transform: uppercase;
	color: var(--eyebrow);
	margin-bottom: 6px;
}

.title-line {
	display: flex;
	align-items: baseline;
	gap: 12px;
	flex-wrap: wrap;
}

.title {
	margin: 0;
	font-size: 23px;
	font-weight: 500;
	letter-spacing: -0.3px;
}

.sub {
	font-size: 12.5px;
	color: var(--text-tertiary);
}

.body {
	flex: 1;
	min-height: 0;
	display: flex;
	flex-direction: column;
	padding: 16px 28px 28px;
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
	color: var(--text-primary);
}

.state-copy {
	margin: 0;
	max-width: 420px;
	font-size: 13px;
	line-height: 1.6;
	color: var(--text-tertiary);
}

.cols {
	flex: 1;
	min-height: 0;
	display: flex;
	gap: 18px;
}

.rail {
	flex: none;
	width: 190px;
	overflow-y: auto;
	display: flex;
	flex-direction: column;
	gap: 2px;
	border-right: 1px solid var(--border-subtle);
	padding-right: 8px;
}

.rail-row {
	display: flex;
	align-items: center;
	gap: 10px;
	padding: 9px 10px;
	border: 1px solid transparent;
	border-radius: var(--border-radius);
	background: transparent;
	color: inherit;
	cursor: pointer;
	text-align: left;

	&:hover {
		background: var(--surface-alpha);
	}

	&.active {
		background: var(--surface-alpha);
		border-color: var(--border-medium);
	}
}

.rail-name {
	font-size: 12.5px;
	color: var(--text-secondary);
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
}

.spine {
	flex: 1;
	min-width: 0;
	overflow-y: auto;
	display: flex;
	flex-direction: column;
	gap: 12px;
	padding-right: 4px;
}

.tier {
	display: flex;
	align-items: center;
	gap: 16px;
	padding: 18px 20px;
	border: 1px solid var(--border-subtle);
	border-radius: var(--border-radius);
	background: var(--surface);
	color: inherit;
	cursor: pointer;
	text-align: left;

	&:hover {
		border-color: var(--border-medium);
	}

	&.active {
		border-color: var(--accent);
	}
}

.tier-meta {
	display: flex;
	flex-direction: column;
	gap: 8px;
	min-width: 0;
}

.tier-name {
	font-size: 14px;
	font-weight: 500;
}

.tier-tags {
	display: flex;
	flex-wrap: wrap;
	gap: 7px;
}

.tag {
	font-family: var(--mono);
	font-size: 10px;
	letter-spacing: 0.5px;
	text-transform: uppercase;
	padding: 3px 8px;
	border-radius: var(--border-radius);
	border: 1px solid var(--border-subtle);
	color: var(--text-tertiary);

	&.state-training {
		color: var(--accent);
		border-color: var(--accent);
	}

	&.state-maxed {
		color: var(--gold);
		border-color: var(--gold);
	}
}
</style>
