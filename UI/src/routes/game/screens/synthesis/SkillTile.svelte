<!-- A square skill tile for the Synthesis bench: a resolved input/result skill renders its icon (or
     initials fallback) tinted by rarity + its primary scaling attribute; an unresolved (masked) slot —
     an input the player hasn't gathered yet on a hinted recipe — renders a dashed "?" so the pairing
     stays a secret (the conservative reveal). -->
<div
	class="tile"
	class:masked={!skill}
	style:--tile-accent={accent}
	style:--tile-border={border}
	style:--tile-font="{Math.round(size * 0.34)}px"
	style:width="{size}px"
	style:height="{size}px"
>
	{#if skill}
		{#if skill.iconPath}
			<div class="clip"><img src={skill.iconPath} alt={skill.name} /></div>
		{:else}
			<span class="initials">{initials}</span>
		{/if}
	{:else}
		<span class="mark" aria-hidden="true">?</span>
	{/if}
</div>

<script lang="ts">
import type { ISkill } from '$lib/api';
import { attributeColor, rarityColor } from '$lib/common';

type Props = {
	/** The resolved skill; omitted for a masked (not-yet-gathered) input slot. */
	skill?: ISkill;
	/** Tile edge length in px. */
	size?: number;
};

const { skill, size = 38 }: Props = $props();

/** Accent glow keyed to the skill's primary scaling attribute (neutral if none / masked). */
const accent = $derived(
	skill && skill.damageMultipliers.length ? attributeColor(skill.damageMultipliers[0].attributeId) : 'var(--accent)'
);

/** The tile edge tinted by rarity (themeable var); neutral when masked. */
const border = $derived(skill ? rarityColor(skill.rarityId) : 'var(--border-light)');

/** Up-to-two-letter fallback when a resolved skill has no authored icon. */
const initials = $derived(
	skill
		? skill.name
				.split(/\s+/)
				.map((w) => w[0] ?? '')
				.join('')
				.slice(0, 2)
				.toUpperCase()
		: ''
);
</script>

<style lang="scss">
.tile {
	position: relative;
	flex-shrink: 0;
	display: flex;
	align-items: center;
	justify-content: center;
	border: 1px solid var(--tile-border, var(--border-light));
	border-radius: 3px;
	background: color-mix(in srgb, var(--white) 4%, transparent);
	overflow: hidden;

	&::before {
		content: '';
		position: absolute;
		inset: 0;
		background: radial-gradient(
			circle at 50% 32%,
			color-mix(in srgb, var(--tile-accent) 32%, transparent),
			transparent 72%
		);
	}

	&.masked {
		border-style: dashed;
		opacity: 0.55;
		&::before {
			display: none;
		}
	}
}

.clip {
	position: absolute;
	inset: 0;
	display: flex;
	align-items: center;
	justify-content: center;

	img {
		position: relative;
		width: 100%;
		height: 100%;
		object-fit: cover;
		opacity: 0.92;
	}
}

.initials {
	position: relative;
	font-family: var(--mono);
	font-weight: 500;
	font-size: var(--tile-font);
	color: var(--text-secondary);
}

.mark {
	position: relative;
	font-family: var(--mono);
	font-size: var(--tile-font);
	color: var(--text-muted);
}
</style>
