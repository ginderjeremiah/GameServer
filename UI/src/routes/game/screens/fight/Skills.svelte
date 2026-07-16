<div
	class="skills-row"
	class:reversed={side === 'enemy'}
	style:--ready-accent={readyAccent}
	use:tutorialAnchor={TOUR_ANCHOR_KEY.fightSkillBar(side)}
>
	{#each battler.skills as skill, index (skill?.id ?? -index - 1)}
		<div class="skill-column">
			{#if skill}
				<!-- Charge sweep/ready computed once per skill per frame (this row re-renders
				     every animation frame for the player, enemy, and boss cards). -->
				{@const charge = chargeProjection(skill.renderChargeTime, skill.cooldownMs)}
				<!-- A focusable button so the per-skill combat tooltip is reachable by keyboard
				     and screen reader, not just on hover. Its accessible name is the icon's alt. -->
				<button
					type="button"
					class="skill-slot"
					class:ready={charge.ready}
					style:--pulse-color={pulseColor}
					onmousemove={handleMouseMove}
					onmouseenter={(ev) => handleEnter(ev, skill.id)}
					onmouseleave={() => handleLeave(skill.id)}
					onfocus={(ev) => handleFocus(ev, skill.id)}
					onblur={() => handleLeave(skill.id)}
					use:describedByTooltip={describedById}
				>
					<img class="skill-icon" src={skill.iconPath} alt={skill.name} />
					<CooldownOverlay sweep={charge.sweep} />
					{#if charge.ready}
						<div class="ready-glow"></div>
					{/if}
					{#if skill.effects.length > 0}
						<div class="effect-badge-anchor"><SkillEffectBadge /></div>
					{/if}
				</button>
				<span class="skill-label" class:ready-label={charge.ready}>{skill.name}</span>
			{:else}
				<div class="skill-slot" aria-hidden="true"></div>
			{/if}
		</div>
	{/each}
	<SkillTooltip bind:this={tooltip} skill={tooltipSkill} />
</div>

<script lang="ts">
import type { Battler } from '$lib/battle';
import { tintColor } from '$lib/common';
import {
	anchorPosition,
	focusAnchor,
	registerTooltipComponent,
	type TooltipAnchor,
	type TooltipComponent
} from '$stores/tooltip.svelte';
import SkillEffectBadge from '$components/SkillEffectBadge.svelte';
import CooldownOverlay from '$components/CooldownOverlay.svelte';
import { describedByTooltip } from '$components/tooltip/describedby-tooltip';
import { tutorialAnchor, TOUR_ANCHOR_KEY } from '$components';
import SkillTooltip from './SkillTooltip.svelte';
import { chargeProjection } from './skill-cooldown';

type Props = {
	battler: Battler;
	side: 'player' | 'enemy';
	/** Overrides the "ready" accent (border/glow/label). Defaults to the brand accent
	 *  so existing player/enemy cards are unchanged; the boss card passes its gold accent. */
	accent?: string;
};

const { battler, side, accent }: Props = $props();

let tooltip = $state<TooltipComponent>();
// Tracked by skill id, not slot index: a battler's skills array is reassigned on every spawn, and the
// keyed {#each} above destroys/recreates a slot's button when the id at that index changes, so a stale
// index would silently re-derive to the new occupant instead of closing (mirrors ActiveEffectChips).
let tooltipSkillId = $state<number>();

const tooltipSkill = $derived(battler.skills.find((skill) => skill?.id === tooltipSkillId));
/** The accent the "ready" border/glow/label resolve to (CSS var, theme-overridable). */
const readyAccent = $derived(accent ?? 'var(--accent)');
const pulseColor = $derived(tintColor(accent ?? (side === 'player' ? 'var(--accent)' : 'var(--enemy-accent)'), 0.6));

const { describedById, setTooltipPosition, showTooltip, hideTooltip } = registerTooltipComponent(() => tooltip);

const handleMouseMove = (ev: MouseEvent) => {
	setTooltipPosition(anchorPosition(ev));
};

// Reveal the per-skill tooltip, anchoring at the cursor (mouse) or the slot's box (focus).
const reveal = (id: number, anchor: TooltipAnchor) => {
	tooltipSkillId = id;
	setTooltipPosition(anchorPosition(anchor));
	showTooltip();
};

const handleEnter = (ev: MouseEvent, id: number) => {
	reveal(id, ev);
};

// Keyboard focus anchors off the slot's box; a mouse click is left to the hover handlers so the
// tooltip keeps tracking the cursor instead of jumping (#880).
const handleFocus = (ev: FocusEvent, id: number) => {
	const anchor = focusAnchor(ev);
	if (anchor) {
		reveal(id, anchor);
	}
};

const handleLeave = (id: number) => {
	if (tooltipSkillId === id) {
		tooltipSkillId = undefined;
		hideTooltip();
	}
};

// Self-heal if the hovered skill vanishes from the loadout without a mouseleave/blur ever firing (e.g.
// a respawn whose new loadout has no skill at all at the old slot).
$effect(() => {
	if (tooltipSkillId != null && !tooltipSkill) {
		tooltipSkillId = undefined;
		hideTooltip();
	}
});
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
	min-width: 64px;
}

.skill-slot {
	// Reset native button chrome (the slot is a <button> so its tooltip is keyboard-reachable);
	// the visual treatment below is shared with the empty placeholder <div>.
	appearance: none;
	margin: 0;
	padding: 0;
	font: inherit;
	color: inherit;
	width: 64px;
	height: 64px;
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

	&:focus-visible {
		outline: 2px solid var(--ready-accent);
		outline-offset: 2px;
	}
}

.skill-icon {
	position: absolute;
	inset: 0;
	width: 100%;
	height: 100%;
	opacity: 0.92;
}

.ready-glow {
	position: absolute;
	inset: -1px;
	border-radius: 2px;
	animation: ready-pulse 1.2s ease-in-out infinite;
	pointer-events: none;
}

.effect-badge-anchor {
	position: absolute;
	top: 3px;
	right: 3px;
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
