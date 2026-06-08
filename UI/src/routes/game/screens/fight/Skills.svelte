<div class="skills-row" class:reversed={side === 'enemy'} style:--ready-accent={readyAccent} role="grid">
	{#each battler.skills as skill, index (skill?.id ?? -index - 1)}
		<div class="skill-column">
			<div
				class="skill-slot"
				class:ready={skill && isReady(skill)}
				style:--skill-sweep="{skillSweep(skill)}deg"
				style:--pulse-color={pulseColor}
				role="gridcell"
				tabindex="-1"
				onmousemove={handleMouseMove}
				onmouseenter={(ev) => handleMouseEnter(ev, index)}
				onmouseleave={(ev) => handleMouseLeave(ev, index)}
			>
				{#if skill}
					<img class="skill-icon" src={skill.iconPath} alt={skill.name} />
					<div class="cooldown-overlay"></div>
					{#if isReady(skill)}
						<div class="ready-glow"></div>
					{/if}
				{/if}
			</div>
			{#if skill}
				<span class="skill-label" class:ready-label={isReady(skill)}>{skill.name}</span>
			{/if}
		</div>
	{/each}
	<SkillTooltip bind:this={tooltip} skill={tooltipSkill} />
</div>

<script lang="ts">
import type { Battler, Skill } from '$lib/battle';
import { formatNum, tintColor } from '$lib/common';
import { registerTooltipComponent, type TooltipComponent } from '$stores/tooltip.svelte';
import SkillTooltip from './SkillTooltip.svelte';

type Props = {
	battler: Battler;
	side: 'player' | 'enemy';
	/** Overrides the "ready" accent (border/glow/label). Defaults to the brand accent
	 *  so existing player/enemy cards are unchanged; the boss card passes its gold accent. */
	accent?: string;
};

const { battler, side, accent }: Props = $props();

let tooltip = $state<TooltipComponent>();
let tooltipSkillIndex = $state(-1);

const tooltipSkill = $derived(battler.skills[tooltipSkillIndex]);
/** The accent the "ready" border/glow/label resolve to (CSS var, theme-overridable). */
const readyAccent = $derived(accent ?? 'var(--accent)');
const pulseColor = $derived(tintColor(accent ?? (side === 'player' ? 'var(--accent)' : 'var(--enemy-accent)'), 0.6));

const { setTooltipPosition, showTooltip, hideTooltip } = registerTooltipComponent(() => tooltip);

const handleMouseMove = (ev: MouseEvent) => {
	setTooltipPosition({ x: ev.clientX, y: ev.clientY });
};

const handleMouseEnter = (ev: MouseEvent, index: number) => {
	tooltipSkillIndex = index;
	if (battler.skills[index]) {
		setTooltipPosition({ x: ev.clientX, y: ev.clientY });
		showTooltip();
	}
};

const handleMouseLeave = (ev: MouseEvent, index: number) => {
	if (tooltipSkillIndex === index) {
		tooltipSkillIndex = -1;
		hideTooltip();
	}
};

const skillPercent = (skill: Skill | undefined) => {
	if (skill) {
		return +formatNum((100 * skill.renderChargeTime) / skill.cooldownMs);
	}
	return 0;
};

const skillSweep = (skill: Skill | undefined) => {
	return (skillPercent(skill) / 100) * 360;
};

const isReady = (skill: Skill) => {
	return skillPercent(skill) >= 99.9;
};
</script>

<style lang="scss">
.skills-row {
	display: flex;
	gap: 10px;
	flex-wrap: wrap;

	&.reversed {
		justify-content: flex-end;
	}
}

.skill-column {
	display: flex;
	flex-direction: column;
	align-items: center;
	gap: 4px;
	min-width: 46px;
}

.skill-slot {
	width: 46px;
	height: 46px;
	position: relative;
	background: color-mix(in srgb, var(--white) 5%, transparent);
	border: 1px solid var(--border-light);
	border-radius: 2px;
	overflow: hidden;
	cursor: default;
	transition:
		border-color 140ms,
		box-shadow 140ms;

	&.ready {
		border-color: color-mix(in srgb, var(--ready-accent) 53%, transparent);
		box-shadow: inset 0 0 8px color-mix(in srgb, var(--ready-accent) 35%, transparent);
	}
}

.skill-icon {
	position: absolute;
	inset: 0;
	width: 100%;
	height: 100%;
	opacity: 0.92;
}

.cooldown-overlay {
	position: absolute;
	inset: 0;
	background: conic-gradient(
		transparent var(--skill-sweep),
		color-mix(in srgb, var(--black) 65%, transparent) var(--skill-sweep)
	);
	pointer-events: none;
}

.ready-glow {
	position: absolute;
	inset: -1px;
	border-radius: 2px;
	animation: ready-pulse 1.2s ease-in-out infinite;
	pointer-events: none;
}

.skill-label {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 0.5px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--text-primary) 60%, transparent);
	white-space: nowrap;

	&.ready-label {
		color: var(--ready-accent);
	}
}
</style>
