<!-- The row of active-effect chips on a battler card: one skill-style icon tile per timed effect
     currently modifying the battler. Effects STACK — re-applying one adds another application — so each
     tile groups all active applications of a single authored effect: it shows the attribute's icon with
     a radial (conic) cooldown overlay tracking the longest-remaining application, the combined-total
     magnitude badge, a buff/debuff direction tint, and (when more than one is active) a count badge in
     the top-right corner. Reads the battler's reactive `activeEffects` projection, so tiles
     appear/expire as effects come and go. -->
{#if effectGroups.length > 0}
	<div class="effect-chips" class:reversed data-testid="effect-chips">
		{#each effectGroups as group (group.sourceId)}
			{@const direction = effectDirection(
				attributeIsHarmful(group.attribute, staticData.attributes),
				group.modifierType,
				group.totalAmount
			)}
			{@const magnitude = formatEffectMagnitude(group.modifierType, group.totalAmount)}
			<!-- A focusable button (mirroring the skill slots) so the attribute tooltip is reachable by
			     keyboard and screen reader, not just on hover. Its accessible name describes the effect. -->
			<button
				type="button"
				class="effect-chip"
				style:--chip-accent={effectDirectionColor(direction)}
				aria-label="{attributeName(group.attribute, staticData.attributes)} {direction}, {magnitude}{group.count > 1
					? `, ${group.count} applications`
					: ''}"
				onmouseenter={(ev) => showChipTooltip(group, ev)}
				onmousemove={(ev) => tip.controller.move(ev)}
				onmouseleave={hideChipTooltip}
				onfocus={(ev) => focusChipTooltip(group, ev)}
				onblur={hideChipTooltip}
				use:describedByTooltip={tip.controller.describedById}
			>
				<AttributeIcon id={group.attribute} size={26} />
				<CooldownOverlay sweep={remainingSweep(group)} />
				{#if group.count > 1}
					<span class="chip-count" data-testid="chip-count">{group.count}</span>
				{/if}
				<span class="chip-mag">{magnitude}</span>
			</button>
		{/each}
	</div>
{/if}

<!-- One tooltip instance per chip row, anchored to whichever chip is hovered/focused. Always mounted
     (it stays hidden until a chip is triggered) so its registration survives chips coming and going.
     The effect context is the live `ActiveEffectView` (resolved by source id), so the tooltip's
     countdown pill keeps depleting and the panel closes itself if the hovered effect expires. -->
<AttributeTooltip bind:this={tooltip} attributeId={tip.attributeId} effect={shownEffectContext} />

<script lang="ts">
import {
	attributeIsHarmful,
	attributeName,
	combineEffectAmount,
	effectDirection,
	effectDirectionColor,
	formatEffectMagnitude
} from '$lib/common';
import { staticData, type TooltipComponent } from '$stores';
import { focusAnchor, type TooltipAnchor } from '$stores/tooltip.svelte';
import AttributeIcon from '$components/AttributeIcon.svelte';
import CooldownOverlay from '$components/CooldownOverlay.svelte';
import AttributeTooltip from '$components/tooltip/AttributeTooltip.svelte';
import { createAttributeTooltip } from '$components/tooltip/attribute-tooltip.svelte';
import { describedByTooltip } from '$components/tooltip/describedby-tooltip';
import type { ActiveEffectView, Battler } from '$lib/battle';
import type { EAttribute, EModifierType } from '$lib/api';

type Props = {
	battler: Battler;
	/** Right-aligns the row to match the enemy/boss card's mirrored layout. */
	reversed?: boolean;
};

const { battler, reversed = false }: Props = $props();

/** All active applications of one authored effect, collapsed into a single chip: the shared
 *  attribute/modifier, the per-application and combined magnitudes, the application count, and the
 *  applications themselves (sorted longest-remaining first) for the tooltip breakdown and the sweep. */
interface EffectGroup {
	sourceId: number;
	attribute: EAttribute;
	modifierType: EModifierType;
	/** Per-application amount (identical across a same-effect stack). */
	stackAmount: number;
	/** Combined magnitude of every active application (additive summed, multiplicative compounded). */
	totalAmount: number;
	count: number;
	/** Authored duration, shared by every application — the sweep/pill denominator. */
	durationMs: number;
	/** Render-interpolated remaining of the longest-lasting application, driving the sweep/pill. */
	maxRenderRemainingMs: number;
	applications: ActiveEffectView[];
}

// Collapse the flat application list into one group per authored effect, preserving first-seen order so
// the chips don't reshuffle as stacks come and go. A linear find suffices — a battler carries only a
// handful of distinct active effects.
const effectGroups = $derived.by<EffectGroup[]>(() => {
	const groups: EffectGroup[] = [];
	for (const effect of battler.activeEffects) {
		let group = groups.find((g) => g.sourceId === effect.sourceId);
		if (!group) {
			group = {
				sourceId: effect.sourceId,
				attribute: effect.attribute,
				modifierType: effect.modifierType,
				stackAmount: effect.amount,
				totalAmount: 0,
				count: 0,
				durationMs: effect.durationMs,
				maxRenderRemainingMs: 0,
				applications: []
			};
			groups.push(group);
		}
		group.count++;
		group.applications.push(effect);
		group.maxRenderRemainingMs = Math.max(group.maxRenderRemainingMs, effect.renderRemainingMs);
	}
	for (const group of groups) {
		group.totalAmount = combineEffectAmount(group.modifierType, group.stackAmount, group.count);
		group.applications.sort((a, b) => b.remainingMs - a.remainingMs);
	}
	return groups;
});

let tooltip = $state<TooltipComponent>();
const tip = createAttributeTooltip(() => tooltip);

// The source id of the chip the tooltip is currently anchored to (if any). Tracked so the live group
// can be re-resolved each tick and so an expiring chip can close the tooltip itself — a chip removed
// under the cursor never fires `mouseleave`, which otherwise left the panel stuck open.
let shownSourceId = $state<number>();
const shownGroup = $derived(shownSourceId != null ? effectGroups.find((g) => g.sourceId === shownSourceId) : undefined);

// The skill an active effect came from: its `sourceId` is the authored skill-effect id, so find the
// skill that owns it. Display-only (never touches battle math); undefined for a retired/unknown skill.
const sourceSkillName = (sourceId: number): string | undefined =>
	staticData.skills?.find((skill) => skill?.effects.some((e) => e.id === sourceId))?.name;

// Live effect context for the panel: reads renderRemainingMs so the countdown pill (and each stacked
// application's breakdown row) depletes smoothly.
const shownEffectContext = $derived(
	shownGroup
		? {
				modifierType: shownGroup.modifierType,
				amount: shownGroup.totalAmount,
				stackAmount: shownGroup.stackAmount,
				durationMs: shownGroup.durationMs,
				remainingMs: shownGroup.maxRenderRemainingMs,
				sourceName: sourceSkillName(shownGroup.sourceId),
				applications: shownGroup.applications.map((a) => ({
					remainingMs: a.renderRemainingMs,
					durationMs: a.durationMs
				}))
			}
		: undefined
);

// Close the tooltip when the chip it's anchored to expires under the cursor (no `mouseleave` fires).
$effect(() => {
	if (shownSourceId != null && !shownGroup) {
		hideChipTooltip();
	}
});

// The transparent (revealed) arc of the radial overlay: the render-interpolated remaining fraction of
// the longest-lasting application's duration, in degrees. Depletes toward 0 as the last application
// expires; jumps back toward 360 when a fresh application restores the longest `renderRemainingMs`.
const remainingSweep = (group: EffectGroup) =>
	group.durationMs > 0 ? Math.max(0, Math.min(360, (group.maxRenderRemainingMs / group.durationMs) * 360)) : 0;

const showChipTooltip = (group: EffectGroup, anchor: TooltipAnchor) => {
	shownSourceId = group.sourceId;
	tip.controller.show(group.attribute, anchor);
};
// Keyboard focus anchors off the chip's box; a mouse click is left to the hover handlers so the
// tooltip keeps tracking the cursor instead of jumping (#880).
const focusChipTooltip = (group: EffectGroup, ev: FocusEvent) => {
	const anchor = focusAnchor(ev);
	if (anchor) {
		showChipTooltip(group, anchor);
	}
};
const hideChipTooltip = () => {
	shownSourceId = undefined;
	tip.controller.hide();
};
</script>

<style lang="scss">
.effect-chips {
	display: flex;
	flex-wrap: wrap;
	gap: 6px;

	&.reversed {
		justify-content: flex-end;
	}
}

.effect-chip {
	// Reset native button chrome (the chip is a <button> so its tooltip is keyboard-reachable),
	// then lay out as a square icon tile mirroring the skill slots.
	appearance: none;
	margin: 0;
	padding: 0;
	font: inherit;
	color: inherit;
	position: relative;
	width: 38px;
	height: 38px;
	display: flex;
	align-items: center;
	justify-content: center;
	border: 1px solid color-mix(in srgb, var(--chip-accent) 45%, transparent);
	border-radius: 2px;
	background: color-mix(in srgb, var(--chip-accent) 10%, transparent);
	overflow: hidden;
	cursor: default;
	transition: border-color 140ms;

	&:focus-visible {
		outline: 2px solid var(--chip-accent);
		outline-offset: 2px;
	}

	:global(.attr-icon) {
		opacity: 0.92;
	}
}

// Stack count, pinned to the top-right corner above the overlay so it stays legible as the wedge
// sweeps in. Shown only when more than one application is active.
.chip-count {
	position: absolute;
	top: 0;
	right: 0;
	min-width: 12px;
	padding: 0 2px;
	font-family: var(--mono);
	font-size: 9px;
	font-weight: 700;
	line-height: 12px;
	text-align: center;
	color: var(--chip-accent);
	background: color-mix(in srgb, var(--black) 60%, transparent);
	border-bottom-left-radius: 3px;
}

// Sits last in the DOM (above the overlay) so the magnitude stays legible as the wedge sweeps in.
.chip-mag {
	position: absolute;
	left: 0;
	right: 0;
	bottom: 0;
	padding: 1px 0;
	font-family: var(--mono);
	font-size: 9px;
	font-weight: 600;
	line-height: 1;
	text-align: center;
	color: var(--chip-accent);
	background: color-mix(in srgb, var(--black) 55%, transparent);
}
</style>
