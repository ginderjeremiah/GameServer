<div class="skills-container round-border" role="grid">
	{#each skills as skill, index}
		<div
			class="skill-box"
			style="{`--skill-perc: ${skillPercent(skill)}%`}"
			role="gridcell"
			tabindex="-1"
			on:mousemove="{handleMouseMove}"
			on:mouseenter="{(ev) => handleMouseEnter(ev, index)}"
			on:mouseleave="{handleMouseLeave}"
		>
			{#if skill}
				<img class="skill" src="{skill.iconPath}" alt="{skill.name}" />
			{/if}
		</div>
	{/each}
	<SkillTooltip bind:this="{$tooltip}" skill="{tooltipSkill}" />
</div>

<script lang="ts">
import { Battler, Skill } from '$lib/battle';
import { formatNum, writableEx, type WritableEx } from '$lib/common';
import { registerTooltipComponent } from '$stores/tooltip';
import SkillTooltip from './SkillTooltip.svelte';

export let battlerStore: WritableEx<Battler | undefined>;

let tooltip = writableEx<SkillTooltip>();
let tooltipSkillIndex: number = -1;

$: skills = $battlerStore?.skills ?? (Array(4).fill(undefined) as (Skill | undefined)[]);
$: tooltipSkill = skills[tooltipSkillIndex];

const { setTooltipPosition, showTooltip, hideTooltip } = registerTooltipComponent(tooltip);

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

const handleMouseLeave = (ev: MouseEvent) => {
	tooltipSkillIndex = -1;
	hideTooltip();
};

const skillPercent = (skill?: Skill) =>
	skill ? formatNum((100 * skill.chargeTime) / skill.cooldownMS) : 0;
</script>

<style lang="scss">
.skills-container {
	background-color: var(--default-title-color);
	display: flex;
	aspect-ratio: 4;
	flex-wrap: wrap;
	overflow: hidden;
	border: var(--default-border);

	.skill-box {
		height: 100%;
		aspect-ratio: 1;
		box-sizing: border-box;
		position: relative;
	}

	.skill-box + .skill-box {
		border-left: var(--default-border);
	}

	.skill {
		width: 100%;
		height: 100%;
	}

	.skill-box::after {
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
