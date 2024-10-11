<div class="skills-container round-border" role="grid">
	{#each skills as skill, index}
		<div
			class="skill-slot"
			style={`--skill-perc: ${skillPercent(skill)}%`}
			role="gridcell"
			tabindex="-1"
			onmousemove={handleMouseMove}
			onmouseenter={(ev) => handleMouseEnter(ev, index)}
			onmouseleave={(ev) => handleMouseLeave(ev, index)}
		>
			{#if skill}
				<img class="skill" src={skill.iconPath} alt={skill.name} />
			{/if}
		</div>
	{/each}
	<SkillTooltip bind:this={tooltip} skill={tooltipSkill} />
</div>

<script lang="ts">
import type { Battler, Skill } from '$lib/battle';
import { formatNum } from '$lib/common';
import { registerTooltipComponent, type TooltipComponent } from '$stores/tooltip.svelte';
import SkillTooltip from './SkillTooltip.svelte';

type Props = {
	battler: Battler | undefined;
};

const { battler }: Props = $props();

let tooltip = $state<TooltipComponent>();
let tooltipSkillIndex = $state(-1);

const skills = $derived(battler?.skills ?? (Array(4).fill(undefined) as (Skill | undefined)[]));
const tooltipSkill = $derived(skills[tooltipSkillIndex]);
const cdr = $derived(battler?.cdMultiplier ?? 1);

const { setTooltipPosition, showTooltip, hideTooltip } = registerTooltipComponent(() => tooltip);

const handleMouseMove = (ev: MouseEvent) => {
	setTooltipPosition({ x: ev.clientX, y: ev.clientY });
};

const handleMouseEnter = (ev: MouseEvent, index: number) => {
	tooltipSkillIndex = index;
	if (skills[index]) {
		setTooltipPosition({ x: ev.clientX, y: ev.clientY });
		showTooltip();
	}
};

const handleMouseLeave = (ev: MouseEvent, index: number) => {
	if (tooltipSkillIndex == index) {
		tooltipSkillIndex = -1;
		hideTooltip();
	}
};

const skillPercent = (skill: Skill | undefined) => {
	if (skill) {
		return formatNum((100 * skill.renderChargeTime) / skill.cooldownMS);
	} else {
		return 0;
	}
};
</script>

<style lang="scss">
.skills-container {
	background-color: var(--container-background-color);
	display: flex;
	aspect-ratio: 4;
	flex-wrap: wrap;
	overflow: hidden;
	border: var(--default-border);

	.skill-slot {
		height: 100%;
		aspect-ratio: 1;
		box-sizing: border-box;
		position: relative;
		background-color: var(--slot-background-color);
	}

	.skill-slot + .skill-slot {
		border-left: var(--default-border);
	}

	.skill {
		width: 100%;
		height: 100%;
	}

	.skill-slot::after {
		content: '';
		position: absolute;
		top: 0;
		right: 0;
		bottom: 0;
		left: 0;
		background: conic-gradient(
			rgb(65, 65, 65, 0),
			rgb(65, 65, 65, 0) var(--skill-perc),
			rgb(65, 65, 65, 0.7) var(--skill-perc),
			rgb(65, 65, 65, 0.7)
		);
	}
}
</style>
