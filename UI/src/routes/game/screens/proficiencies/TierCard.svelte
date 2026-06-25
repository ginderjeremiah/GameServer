<!-- One tier on a path's spine: a connector column (state-coloured node diamond + dashed line down to
     the next tier) beside a card whose word of power illuminates with its decipher stage — dim when
     undeciphered, accent once its pronunciation is learned, gold when fully translated — over a reveal
     line, level/milestone pip track, and training/level tags. Hovering surfaces the decipher tooltip. -->
<div class="tier-row">
	<div class="connector">
		<div class="node state-{tier.state}"></div>
		{#if !last}
			<div class="line"></div>
		{/if}
	</div>

	<button
		type="button"
		class="card"
		class:selected
		aria-current={selected ? 'true' : undefined}
		data-testid="tier-{tier.id}"
		onclick={() => onSelect(tier.id)}
		use:wordHover={{ controller, tier }}
		use:describedByTooltip={controller.describedById}
	>
		<div class="card-inner">
			<div class="icon-chip" class:dim={tier.decipher === 'undeciphered'}>
				{#if tier.iconPath}
					<!-- Decorative: the glyph's WordOfPower already carries the tier name for assistive tech. -->
					<img src={tier.iconPath} alt="" />
				{/if}
			</div>

			<div class="card-body">
				<WordOfPower
					class="glyph stage-{tier.decipher}"
					text={tier.word}
					label={tier.name}
					size={33}
					glow={tier.decipher !== 'undeciphered'}
				/>
				<div class="reveal stage-{tier.decipher}">{reveal}</div>
			</div>

			<div class="meta">
				<PipTrack level={tier.level} maxLevel={tier.maxLevel} milestoneLevels={tier.milestoneLevels} />
				<div class="tags">
					{#if tier.state === 'training'}
						<span class="training-tag">training</span>
					{/if}
					<span class="level-tag">{tier.level}/{tier.maxLevel}</span>
				</div>
			</div>
		</div>
	</button>
</div>

<script lang="ts">
import { WordOfPower } from '$components';
import { describedByTooltip } from '$components/tooltip/describedby-tooltip';
import PipTrack from './PipTrack.svelte';
import type { TierView } from './proficiencies-lexicon';
import { decipherReveal } from './word-detail';
import { wordHover, type WordTooltipController } from './word-hover';

interface Props {
	/** The tier to render. */
	tier: TierView;
	/** Whether this is the selected tier (accent border + inset). */
	selected: boolean;
	/** True for the root (bottom-most) card, which draws no connector line below it. */
	last: boolean;
	/** Select this tier (drives the rail/inspector selection). */
	onSelect: (id: number) => void;
	/** Drives the shared decipher tooltip on hover/focus. */
	controller: WordTooltipController;
}

const { tier, selected, last, onSelect, controller }: Props = $props();

const reveal = $derived(decipherReveal(tier));
</script>

<style lang="scss">
.tier-row {
	display: flex;
	gap: 16px;
	align-items: stretch;
}

// Connector column: the node diamond sits beside the card's vertical centre, with a dashed line
// dropping to the next tier below.
.connector {
	flex: none;
	width: 18px;
	display: flex;
	flex-direction: column;
	align-items: center;
	padding-top: 30px;
}

.node {
	flex: none;
	width: 13px;
	height: 13px;
	transform: rotate(45deg);
	border: 1.5px solid color-mix(in srgb, var(--white) 30%, transparent);
	background: transparent;

	&.state-training {
		border-color: var(--accent);
		background: var(--accent);
		box-shadow: 0 0 8px color-mix(in srgb, var(--accent) 60%, transparent);
	}

	&.state-maxed {
		border-color: var(--gold);
		background: var(--gold);
		box-shadow: 0 0 7px color-mix(in srgb, var(--gold) 50%, transparent);
	}
}

.line {
	flex: 1;
	width: 1px;
	min-height: 46px;
	margin: 6px 0;
	background: repeating-linear-gradient(color-mix(in srgb, var(--white) 20%, transparent) 0 4px, transparent 4px 9px);
}

.card {
	flex: 1;
	min-width: 0;
	padding: 24px 26px;
	text-align: left;
	cursor: pointer;
	border: 1px solid var(--border-light);
	border-radius: var(--border-radius);
	background: color-mix(in srgb, var(--white) 3%, transparent);
	color: inherit;
	transition:
		border-color 130ms,
		box-shadow 130ms;

	&:hover {
		border-color: color-mix(in srgb, var(--accent) 60%, transparent);
		box-shadow: 0 0 0 2px color-mix(in srgb, var(--accent) 45%, transparent);
	}

	&.selected {
		border-color: color-mix(in srgb, var(--accent) 55%, transparent);
		background: color-mix(in srgb, var(--accent) 7%, transparent);
		box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--accent) 25%, transparent);
	}
}

.card-inner {
	display: flex;
	align-items: center;
	gap: 14px;
}

.icon-chip {
	flex: none;
	width: 38px;
	height: 38px;
	display: flex;
	align-items: center;
	justify-content: center;
	border: 1px dashed var(--border-medium);
	border-radius: var(--border-radius);
	background: color-mix(in srgb, var(--white) 3%, transparent);
	overflow: hidden;

	&.dim {
		opacity: 0.55;
	}

	img {
		width: 100%;
		height: 100%;
		object-fit: contain;
	}
}

.card-body {
	flex: 1;
	min-width: 0;
}

// Word-of-power illumination by decipher stage. The glyph is the child WordOfPower element, so its
// classes are reached through `:global()` (anchored under `.card-body` so the rule isn't global-leaky).
// WordOfPower inherits its colour, so the stage hue drives both the glyph and (via currentColor) its glow.
.card-body :global(.glyph) {
	display: block;
	line-height: 1.05;
}

.card-body :global(.glyph.stage-undeciphered) {
	color: var(--text-tertiary);
}

.card-body :global(.glyph.stage-pronunciation) {
	color: var(--accent-light);
}

.card-body :global(.glyph.stage-translated) {
	color: var(--gold);
}

.reveal {
	margin-top: 6px;
	font-family: var(--mono);
	font-size: 12px;
	letter-spacing: 0.4px;

	&.stage-undeciphered {
		color: var(--text-muted);
		font-style: italic;
	}

	&.stage-pronunciation {
		color: var(--accent-light);
	}

	&.stage-translated {
		color: var(--gold);
	}
}

.meta {
	flex: none;
	display: flex;
	flex-direction: column;
	align-items: flex-end;
	gap: 6px;
}

.tags {
	display: flex;
	align-items: center;
	gap: 8px;
}

.training-tag {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.4px;
	text-transform: uppercase;
	color: var(--accent);
}

.level-tag {
	font-family: var(--mono);
	font-size: 12px;
	letter-spacing: 0.5px;
	color: var(--text-secondary);
}
</style>
