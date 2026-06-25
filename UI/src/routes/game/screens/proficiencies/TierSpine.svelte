<!-- The centre column for the selected path: a header (the path's root word of power + how many words it
     holds) over the spine of tier cards, drawn most-advanced at the top down to the root at the bottom. -->
<div class="spine-col">
	<div class="path-header">
		<div class="path-icon">
			{#if path.iconPath}
				<!-- Decorative: the path word's WordOfPower already carries the path name for assistive tech. -->
				<img src={path.iconPath} alt="" />
			{/if}
		</div>
		<div class="path-text">
			<WordOfPower class="path-word" text={path.word} label={path.name} size={46} />
			<div class="path-sub">{path.name} · {wordCount}</div>
		</div>
	</div>

	<div class="spine">
		{#each spineTiers as tier, i (tier.id)}
			<TierCard
				{tier}
				selected={tier.id === selectedTierId}
				last={i === spineTiers.length - 1}
				{onSelect}
				{controller}
			/>
		{/each}
	</div>
</div>

<script lang="ts">
import { WordOfPower } from '$components';
import TierCard from './TierCard.svelte';
import type { PathView } from './proficiencies-lexicon';
import type { WordTooltipController } from './word-hover';

interface Props {
	/** The selected path whose spine to render. */
	path: PathView;
	/** The selected tier within the path, if any. */
	selectedTierId: number | undefined;
	/** Select a tier on the spine. */
	onSelect: (id: number) => void;
	/** Drives the shared decipher tooltip on hover/focus. */
	controller: WordTooltipController;
}

const { path, selectedTierId, onSelect, controller }: Props = $props();

// The view-model orders tiers root-first (ascending pathOrdinal); the spine draws most-advanced first.
const spineTiers = $derived([...path.tiers].reverse());
const wordCount = $derived(path.tiers.length === 1 ? '1 WORD KNOWN' : `${path.tiers.length} WORDS KNOWN`);
</script>

<style lang="scss">
.spine-col {
	min-width: 440px;
	padding: 26px 36px 38px;
}

.path-header {
	display: flex;
	align-items: center;
	gap: 14px;
	padding-bottom: 18px;
	border-bottom: 1px solid var(--border-subtle);
}

.path-icon {
	flex: none;
	width: 48px;
	height: 48px;
	display: flex;
	align-items: center;
	justify-content: center;
	border: 1px dashed var(--border-medium);
	border-radius: var(--border-radius);
	background: color-mix(in srgb, var(--white) 3%, transparent);
	overflow: hidden;

	img {
		width: 100%;
		height: 100%;
		object-fit: contain;
	}
}

.path-text {
	flex: 1;
	min-width: 0;
}

// The path word is the child WordOfPower element, reached through `:global()` anchored under
// `.path-text` so the rule stays scoped to this component's subtree.
.path-text :global(.path-word) {
	display: block;
	line-height: 1;
	color: var(--text-primary);
}

.path-sub {
	margin-top: 7px;
	font-family: var(--mono);
	font-size: 11px;
	letter-spacing: 0.8px;
	color: var(--text-tertiary);
}

.spine {
	margin-top: 20px;
	display: flex;
	flex-direction: column;
	gap: 16px;
}
</style>
