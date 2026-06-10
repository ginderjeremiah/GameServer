<div class="skill-icon" class:locked style:--icon-accent={accent} style:width="{size}px" style:height="{size}px">
	{#if locked}
		<span class="lock-glyph" aria-hidden="true">🔒</span>
	{:else}
		<img src={skill.iconPath} alt={skill.name} />
	{/if}
</div>

<script lang="ts">
import type { ISkill } from '$lib/api';
import { attributeColor } from '$lib/common';

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
</script>

<style lang="scss">
.skill-icon {
	position: relative;
	display: flex;
	align-items: center;
	justify-content: center;
	flex-shrink: 0;
	overflow: hidden;
	border: 1px solid var(--border-light);
	border-radius: 3px;
	background: color-mix(in srgb, var(--white) 4%, transparent);

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

	&.locked {
		opacity: 0.8;
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
</style>
