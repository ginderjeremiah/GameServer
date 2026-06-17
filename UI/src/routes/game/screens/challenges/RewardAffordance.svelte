{#if !reward}
	<span class="no-reward">No reward</span>
{:else if variant === 'chip'}
	<button
		type="button"
		class="chip"
		class:hovered
		aria-label={ariaLabel}
		style:background={tintColor(accent, hovered ? 0.16 : 0.08)}
		style:border="1px {reward.revealed ? 'solid' : 'dashed'}
		{tintColor(accent, hovered ? 0.6 : 0.34)}"
		style:box-shadow={glow && hovered ? `0 0 12px ${tintColor(accent, 0.4)}` : 'none'}
		onmouseenter={onEnter}
		onmousemove={onMove}
		onmouseleave={onLeave}
		onfocus={onFocus}
		onblur={onLeave}
		use:describedByTooltip={tooltip?.describedById}
	>
		<RewardIcon {reward} size={18} {glow} />
		<span class="chip-name" style:color={accent}>{reward.revealed ? reward.name : '???'}</span>
	</button>
{:else}
	<button
		type="button"
		class="tile"
		aria-label={ariaLabel}
		style:background={tintColor(accent, hovered ? 0.1 : 0.05)}
		style:border="1px {reward.revealed ? 'solid' : 'dashed'}
		{tintColor(accent, hovered ? 0.6 : 0.3)}"
		onmouseenter={onEnter}
		onmousemove={onMove}
		onmouseleave={onLeave}
		onfocus={onFocus}
		onblur={onLeave}
		use:describedByTooltip={tooltip?.describedById}
	>
		<RewardIcon {reward} size={46} {glow} animate={!reward.revealed} />
		<div class="tile-text">
			<div
				class="tile-name"
				class:sealed={!reward.revealed}
				style:color={accent}
				style:text-shadow={reward.revealed && glow && hovered ? `0 0 12px ${tintColor(accent, 0.6)}` : 'none'}
			>
				{reward.revealed ? reward.name : '???'}
			</div>
			<div class="tile-sub">{reward.sub}</div>
		</div>
		<svg
			class="tile-info"
			aria-hidden="true"
			width="13"
			height="13"
			viewBox="0 0 16 16"
			fill="none"
			stroke={tintColor(accent, hovered ? 0.9 : 0.4)}
			stroke-width="1.4"
		>
			<circle cx="8" cy="8" r="6" />
			<path d="M8 5.2v.2M8 7.4v3.4" stroke-linecap="round" />
		</svg>
	</button>
{/if}

<script lang="ts">
import { tintColor } from '$lib/common';
import { describedByTooltip } from '$components/tooltip/describedby-tooltip';
import RewardIcon from './RewardIcon.svelte';
import { getRewardTooltip } from './reward-tooltip-context';
import type { ResolvedReward } from './challenges-view.svelte';

interface Props {
	reward: ResolvedReward | null;
	variant?: 'tile' | 'chip';
	glow?: boolean;
}

const { reward, variant = 'tile', glow = true }: Props = $props();

const tooltip = getRewardTooltip();

let hovered = $state(false);

const accent = $derived(reward?.accent ?? 'var(--text-tertiary)');

// Describe the reward for assistive tech: the revealed name, or a sealed teaser.
const ariaLabel = $derived(
	reward == null
		? undefined
		: reward.revealed
			? `Reward: ${reward.name}, ${reward.sub}`
			: `Sealed reward: ${reward.sub}`
);

const onEnter = (ev: MouseEvent) => {
	hovered = true;
	if (reward) {
		tooltip?.show(reward, ev);
	}
};
const onMove = (ev: MouseEvent) => tooltip?.move(ev);
// Keyboard focus opens the same tooltip, positioned off the element's box.
const onFocus = (ev: FocusEvent) => {
	hovered = true;
	if (reward && ev.currentTarget instanceof HTMLElement) {
		tooltip?.show(reward, ev.currentTarget);
	}
};
const onLeave = () => {
	hovered = false;
	tooltip?.hide();
};
</script>

<style lang="scss">
.no-reward {
	font-size: 12.5px;
	color: var(--text-muted);
	font-style: italic;
}

.chip,
.tile {
	// Reset native button chrome; the visual treatment is the inline rarity styles.
	appearance: none;
	margin: 0;
	font: inherit;
	color: inherit;
	text-align: left;
	cursor: help;

	&:focus-visible {
		outline: 2px solid var(--accent);
		outline-offset: 2px;
	}
}

.chip {
	display: inline-flex;
	align-items: center;
	gap: 8px;
	padding: 4px 11px 4px 5px;
	max-width: 100%;
	border-radius: 2px;
	transition: all 120ms;
}

.chip-name {
	font-size: 12.5px;
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
	letter-spacing: 0.1px;
}

.tile {
	display: flex;
	align-items: center;
	gap: 13px;
	padding: 10px 12px;
	width: 100%;
	border-radius: 3px;
	transition: all 130ms;
}

.tile-text {
	min-width: 0;
	flex: 1;
}

.tile-name {
	font-size: 15px;
	white-space: nowrap;
	overflow: hidden;
	text-overflow: ellipsis;
	letter-spacing: -0.1px;
	transition: text-shadow 130ms;

	&.sealed {
		letter-spacing: 1px;
	}
}

.tile-sub {
	margin-top: 4px;
	font-family: var(--mono);
	font-size: 8px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-muted);
}

.tile-info {
	flex-shrink: 0;
	transition: stroke 120ms;
}
</style>
