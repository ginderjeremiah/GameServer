<!-- The shared "word of power" tooltip for the Lexicon spine: surfaces a tier's decipher state — its
     Aetheric glyph plus the pronunciation/translation as they are unsealed by levelling — through the
     standard game tooltip chrome (not a native title). One instance is mounted by the screen and
     relocated into the global tooltip container; the hovered tier is fed in by the screen's controller. -->
<TooltipShell {accent} hidden={!tier} bind:base={container}>
	{#snippet header()}
		<div class="wt-header">
			<div class="wt-label-row">
				<span class="wt-diamond" style:background={accent} style:box-shadow="0 0 6px {accent}"></span>
				<span class="wt-label" style:color={accent}>Word of Power</span>
			</div>
			{#if tier}
				<WordOfPower
					class="wt-word"
					text={tier.word}
					label={tier.name}
					size={24}
					glow={tier.decipher === 'translated'}
				/>
			{/if}
		</div>
	{/snippet}

	{#if tier}
		<div class="wt-rows">
			<div class="wt-row">
				<span class="wt-key">Pron</span>
				<span class="wt-val" class:sealed={!pronKnown} style:color={pronKnown ? 'var(--accent-light)' : undefined}>
					{pronKnown ? `“${tier.pronunciation}”` : '— sealed —'}
				</span>
			</div>
			<div class="wt-row">
				<span class="wt-key">Means</span>
				<span class="wt-val" class:sealed={!transKnown} style:color={transKnown ? 'var(--gold)' : undefined}>
					{transKnown ? tier.translation : '— sealed —'}
				</span>
			</div>
			<p class="wt-hint">{hint}</p>
		</div>
	{/if}
</TooltipShell>

<script lang="ts">
import { WordOfPower } from '$components';
import TooltipShell from '$components/tooltip/TooltipShell.svelte';
import type { TierView } from './proficiencies-lexicon';

interface Props {
	/** The hovered tier whose decipher state to show, or undefined for the hidden panel. */
	tier: TierView | undefined;
}

const { tier }: Props = $props();

// Bound to the shell's root and relocated into the global tooltip container by getBaseNode().
let container = $state<HTMLDivElement>();
export const getBaseNode = () => container;

const accent = $derived(tier?.state === 'maxed' ? 'var(--gold)' : 'var(--accent)');
const pronKnown = $derived(tier != null && tier.decipher !== 'undeciphered');
const transKnown = $derived(tier?.decipher === 'translated');

const hint = $derived.by(() => {
	if (!tier) {
		return '';
	}
	if (tier.decipher === 'undeciphered') {
		return 'Undeciphered — keep training to learn its pronunciation.';
	}
	if (tier.decipher === 'pronunciation') {
		return 'Master this word to translate its meaning.';
	}
	return 'Fully deciphered.';
});
</script>

<style lang="scss">
.wt-header {
	padding: 12px 16px 10px;
	border-bottom: 1px solid color-mix(in srgb, var(--text-primary) 8%, transparent);
}

.wt-label-row {
	display: flex;
	align-items: center;
	gap: 8px;
	margin-bottom: 6px;
}

.wt-diamond {
	width: 5px;
	height: 5px;
	transform: rotate(45deg);
	flex-shrink: 0;
}

.wt-label {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.8px;
	text-transform: uppercase;
}

// The word is the child WordOfPower element, reached through `:global()` anchored under `.wt-header`
// so the rule stays scoped to this component's subtree.
.wt-header :global(.wt-word) {
	color: var(--text-primary);
	line-height: 1.1;
}

.wt-rows {
	display: flex;
	flex-direction: column;
	gap: 7px;
}

.wt-row {
	display: flex;
	justify-content: space-between;
	gap: 10px;
	align-items: baseline;
}

.wt-key {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.2px;
	text-transform: uppercase;
	color: var(--text-tertiary);
}

.wt-val {
	font-family: var(--mono);
	font-size: 11px;
	color: var(--text-primary);
	font-weight: 500;
	text-align: right;

	&.sealed {
		color: var(--text-muted);
		font-weight: 400;
	}
}

.wt-hint {
	margin: 4px 0 0;
	font-size: 11px;
	line-height: 1.5;
	color: var(--text-tertiary);
}
</style>
