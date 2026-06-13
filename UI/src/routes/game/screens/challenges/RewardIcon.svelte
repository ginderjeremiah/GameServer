{#if reward.revealed}
	<div
		class="reveal-icon"
		style:width="{size}px"
		style:height="{size}px"
		style:background={tintColor(accent, 0.08)}
		style:border="1px solid {tintColor(accent, 0.55)}"
		style:box-shadow={glow ? revealGlow : 'none'}
	>
		{#if reward.kind === 'item' && reward.item}
			<CategoryGlyph cat={reward.item.itemCategoryId} color={accent} size={Math.round(size * 0.5)} />
		{:else if reward.kind === 'skill' && reward.skill}
			<img class="skill-img" src={reward.skill.iconPath} alt="" />
		{:else}
			<div
				class="mod-diamond"
				style:width="{size * 0.32}px"
				style:height="{size * 0.32}px"
				style:border="1.5px solid {accent}"
				style:box-shadow="0 0 6px {tintColor(accent, 0.6)}"
			></div>
		{/if}
	</div>
{:else}
	<div
		class="seal-icon"
		class:animate
		style:width="{size}px"
		style:height="{size}px"
		style:background="repeating-linear-gradient(45deg, {tintColor(accent, 0.06)}, {tintColor(accent, 0.06)} 4px, transparent
		4px, transparent 8px)"
		style:border="1px dashed {tintColor(accent, 0.5)}"
		style:--seal-glow={glow ? tintColor(accent, 0.5) : 'transparent'}
	>
		<svg width={size * 0.42} height={size * 0.42} viewBox="0 0 16 16" fill="none" stroke={accent} stroke-width="1.3">
			<rect x="3.5" y="7" width="9" height="6.5" rx="1" />
			<path d="M5.5 7V5.2a2.5 2.5 0 0 1 5 0V7" />
		</svg>
		{#if animate}<span class="seal-sweep"></span>{/if}
	</div>
{/if}

<script lang="ts">
import { tintColor } from '$lib/common';
import CategoryGlyph from '../inventory/CategoryGlyph.svelte';
import type { ResolvedReward } from './challenges-view.svelte';

interface Props {
	reward: ResolvedReward;
	size: number;
	glow?: boolean;
	/** Sealed-only: the slow breathing glow + "waiting to be opened" sweep (tile variant). */
	animate?: boolean;
}

const { reward, size, glow = true, animate = false }: Props = $props();

const accent = $derived(reward.accent);
// Blur radius/alpha scale with the rarity tier's glow intensity (themeable var).
const revealGlow = $derived(`0 0 calc(4px + ${reward.glow} * 16px) ${tintColor(accent, 0.55)}`);
</script>

<style lang="scss">
.reveal-icon,
.seal-icon {
	flex-shrink: 0;
	border-radius: 3px;
	display: flex;
	align-items: center;
	justify-content: center;
}

.seal-icon {
	position: relative;
	overflow: hidden;
}

.mod-diamond {
	transform: rotate(45deg);
}

.skill-img {
	width: 62%;
	height: 62%;
	object-fit: cover;
	border-radius: 3px;
}

@media (prefers-reduced-motion: no-preference) {
	// Sealed reward: a slow breathing rarity glow + a light sweep crossing the tile.
	.seal-icon.animate {
		animation: seal-pulse 2.8s ease-in-out infinite;
	}

	@keyframes seal-pulse {
		0%,
		100% {
			box-shadow: 0 0 3px var(--seal-glow);
		}
		50% {
			box-shadow: 0 0 11px var(--seal-glow);
		}
	}

	.seal-sweep {
		position: absolute;
		inset: 0;
		pointer-events: none;
		background: linear-gradient(
			115deg,
			transparent 38%,
			color-mix(in srgb, var(--white) 16%, transparent) 50%,
			transparent 62%
		);
		transform: translateX(-130%);
		animation: seal-sweep 3.4s ease-in-out infinite;
	}

	@keyframes seal-sweep {
		0% {
			transform: translateX(-130%);
		}
		55%,
		100% {
			transform: translateX(130%);
		}
	}
}
</style>
