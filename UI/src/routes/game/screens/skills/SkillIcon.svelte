<div
	class="skill-icon"
	class:locked
	style:--icon-accent={accent}
	style:--rarity-border={rarityBorder}
	style:width="{size}px"
	style:height="{size}px"
>
	<div class="icon-clip">
		{#if locked}
			<span class="lock-glyph" aria-hidden="true">🔒</span>
		{:else}
			<img src={skill.iconPath} alt={skill.name} />
		{/if}
	</div>
	{#if skill.effects.length > 0}
		<div class="effect-badge-anchor"><SkillEffectBadge /></div>
	{/if}
</div>

<script lang="ts">
import type { ISkill } from '$lib/api';
import { attributeColor, rarityColor } from '$lib/common';
import SkillEffectBadge from '$components/SkillEffectBadge.svelte';

type Props = {
	skill: ISkill;
	/** Whether the skill is locked (shows a lock glyph instead of the icon). */
	locked?: boolean;
	/** Tile edge length in px. */
	size?: number;
};

const { skill, locked = false, size = 30 }: Props = $props();

/** Accent glow keyed to the skill's primary scaling attribute (neutral if none). */
const accent = $derived(
	skill.damageMultipliers.length ? attributeColor(skill.damageMultipliers[0].attributeId) : 'var(--accent)'
);

/** The tile edge tinted by the skill's rarity tier (themeable var), mirroring item tiles. */
const rarityBorder = $derived(rarityColor(skill.rarityId));
</script>

<style lang="scss">
.skill-icon {
	position: relative;
	flex-shrink: 0;
	border: 1px solid var(--rarity-border, var(--border-light));
	border-radius: 3px;
	background: color-mix(in srgb, var(--white) 4%, transparent);

	&.locked {
		opacity: 0.8;
	}
}

// Clips the rounded icon image; kept separate from `.skill-icon` so the effect badge
// (anchored to the non-clipped outer tile) and its glow aren't cut off (#421).
.icon-clip {
	position: absolute;
	inset: 0;
	display: flex;
	align-items: center;
	justify-content: center;
	overflow: hidden;
	border-radius: 3px;

	&::before {
		content: '';
		position: absolute;
		inset: 0;
		background: radial-gradient(
			circle at 50% 32%,
			color-mix(in srgb, var(--icon-accent) 34%, transparent),
			transparent 72%
		);
	}

	img {
		position: relative;
		width: 100%;
		height: 100%;
		object-fit: cover;
		opacity: 0.92;
	}
}

.lock-glyph {
	position: relative;
	font-size: 0.9em;
	color: var(--text-muted);
}

.effect-badge-anchor {
	position: absolute;
	top: 3px;
	right: 3px;
	pointer-events: none;
}
</style>
