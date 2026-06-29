<!-- Proficiencies screen — "The Lexicon".

     The mastery tree rendered as per-path spines (#1217): a rail of discovered paths beside the selected
     path's spine, whose tiers are words of power that illuminate as they decipher, beside a word-detail
     inspector for the selected tier (#1218). This screen owns the rail, the spine, the inspector, and the
     shared decipher tooltip the spine cards and the inspector drive on hover.

     Progress is re-fetched on mount (mirroring the Statistics screen) so the lexicon reflects play since
     the store was last loaded at game boot; the reference data is already in `staticData`. -->
<div class="prof-frame" data-testid="proficiencies-screen">
	<div class="header">
		<div class="eyebrow">Character · Proficiencies</div>
		<div class="title-line">
			<h1 class="title">The Lexicon</h1>
			<span class="sub">paths of mastery, deciphered as you train</span>
			<div class="header-spacer"></div>
			<SynthesisLink />
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
				<LexiconRail paths={view.paths} selectedId={view.selectedPath.id} onSelect={(id) => view.selectPath(id)} />
				<div class="journey">
					<TierSpine
						path={view.selectedPath}
						selectedTierId={view.selectedTier?.id}
						onSelect={(id) => view.selectTier(id)}
						{controller}
					/>
				</div>
				{#if view.selectedTier}
					<WordDetail tier={view.selectedTier} path={view.selectedPath} {controller} />
				{/if}
			</div>
		{/if}
	</div>

	{#if view.loading}
		<Loading delay={100} />
	{/if}

	<WordOfPowerTooltip bind:this={tooltip} tier={hoveredTier} />
</div>

<script lang="ts">
import { onMount } from 'svelte';
import { Loading } from '$components';
import {
	anchorPosition,
	playerProficiencies,
	registerTooltipComponent,
	toastError,
	type TooltipComponent
} from '$stores';
import LexiconRail from './LexiconRail.svelte';
import TierSpine from './TierSpine.svelte';
import WordDetail from './WordDetail.svelte';
import WordOfPowerTooltip from './WordOfPowerTooltip.svelte';
import SynthesisLink from '../synthesis/SynthesisLink.svelte';
import type { TierView, WordTooltipController } from './proficiencies-lexicon';
import { ProficienciesView } from './proficiencies-view.svelte';

const view = new ProficienciesView();

// One decipher tooltip for the whole spine, driven through a controller passed down to the tier cards
// (mirroring the challenges reward tooltip) so the cards don't thread hover handlers back up.
let tooltip = $state<TooltipComponent>();
let hoveredTier = $state<TierView | undefined>();
const { describedById, setTooltipPosition, showTooltip, hideTooltip } = registerTooltipComponent(() => tooltip);
const controller: WordTooltipController = {
	describedById,
	show: (tier, anchor) => {
		hoveredTier = tier;
		setTooltipPosition(anchorPosition(anchor));
		showTooltip();
	},
	move: (anchor) => setTooltipPosition(anchorPosition(anchor)),
	hide: () => {
		hoveredTier = undefined;
		hideTooltip();
	}
};

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
	padding: 18px 24px 14px;
	flex-shrink: 0;
	border-bottom: 1px solid var(--border-subtle);
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

.header-spacer {
	flex: 1;
}

.body {
	flex: 1;
	min-height: 0;
	display: flex;
	flex-direction: column;
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
}

// The scrolling centre column hosting the spine.
.journey {
	flex: 1;
	min-width: 0;
	overflow: auto;
}
</style>
