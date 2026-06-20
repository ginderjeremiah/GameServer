<!-- The row of active-effect chips on a battler card: one skill-style icon tile per (attribute, modifier
     type) currently modifying the battler. Effects STACK — re-applying one adds another application, and
     all applications on an attribute share a single expiry (#992) — so each tile groups every active
     application of one attribute+type: it shows the attribute's icon with a radial (conic) cooldown
     overlay tracking the shared remaining, the combined-total magnitude badge, a buff/debuff direction
     tint, and (when more than one is active) a count badge in the top-right corner. Reads the battler's
     reactive `activeEffects` projection, so tiles appear/expire as effects come and go. -->
{#if effectGroups.length > 0}
	<div class="effect-chips" class:reversed data-testid="effect-chips">
		{#each effectGroups as group (group.key)}
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
import { EModifierType, type EAttribute } from '$lib/api';

type Props = {
	battler: Battler;
	/** Right-aligns the row to match the enemy/boss card's mirrored layout. */
	reversed?: boolean;
};

const { battler, reversed = false }: Props = $props();

/** All active applications of one (attribute, modifier type), collapsed into a single chip: the shared
 *  attribute/modifier, the combined magnitude, the application count, the shared remaining (every
 *  application on an attribute expires together, #992), and the applications themselves (in application
 *  order) for the tooltip breakdown. */
interface EffectGroup {
	/** Stable per-chip key — applications sharing an attribute+type collapse into one chip. */
	key: string;
	attribute: EAttribute;
	modifierType: EModifierType;
	/** Combined magnitude of every active application (additive summed, multiplicative compounded). */
	totalAmount: number;
	count: number;
	/** Duration of the most-recent application — the sweep/pill denominator the shared remaining resets to. */
	durationMs: number;
	/** Render-interpolated shared remaining (all applications share one expiry), driving the sweep/pill. */
	renderRemainingMs: number;
	applications: ActiveEffectView[];
}

// Collapse the flat application list into one group per (attribute, modifier type), preserving first-seen
// order so the chips don't reshuffle as stacks come and go. A linear find suffices — a battler carries
// only a handful of distinct active effects.
const effectGroups = $derived.by<EffectGroup[]>(() => {
	const groups: EffectGroup[] = [];
	for (const effect of battler.activeEffects) {
		let group = groups.find((g) => g.attribute === effect.attribute && g.modifierType === effect.modifierType);
		if (!group) {
			group = {
				key: `${effect.attribute}:${effect.modifierType}`,
				attribute: effect.attribute,
				modifierType: effect.modifierType,
				totalAmount: effect.modifierType === EModifierType.Multiplicative ? 1 : 0,
				count: 0,
				durationMs: effect.durationMs,
				renderRemainingMs: 0,
				applications: []
			};
			groups.push(group);
		}
		group.count++;
		group.applications.push(effect);
		// Additive amounts sum; multiplicative factors compound — matching the battle math, and read from
		// each application's own amount (they can differ when caster-scaling shifts between fires).
		group.totalAmount =
			effect.modifierType === EModifierType.Multiplicative
				? group.totalAmount * effect.amount
				: group.totalAmount + effect.amount;
		group.renderRemainingMs = Math.max(group.renderRemainingMs, effect.renderRemainingMs);
		// activeEffects is in application order, so the last one seen is the most recent — its duration is
		// what the shared remaining was reset to, and so the sweep/pill denominator.
		group.durationMs = effect.durationMs;
	}
	return groups;
});

let tooltip = $state<TooltipComponent>();
const tip = createAttributeTooltip(() => tooltip);

// The key of the chip the tooltip is currently anchored to (if any). Tracked so the live group can be
// re-resolved each tick and so an expiring chip can close the tooltip itself — a chip removed under the
// cursor never fires `mouseleave`, which otherwise left the panel stuck open.
let shownKey = $state<string>();
const shownGroup = $derived(shownKey != null ? effectGroups.find((g) => g.key === shownKey) : undefined);

// The skill an active effect came from: its `sourceId` is the authored skill-effect id, so find the
// skill that owns it. Display-only (never touches battle math); undefined for a retired/unknown skill.
const sourceSkillName = (sourceId: number): string | undefined =>
	staticData.skills?.find((skill) => skill?.effects.some((e) => e.id === sourceId))?.name;

// Live effect context for the panel: reads renderRemainingMs so the countdown pill depletes smoothly. A
// single application names its source in the header; a stack names each application's source per row.
const shownEffectContext = $derived(
	shownGroup
		? {
				modifierType: shownGroup.modifierType,
				amount: shownGroup.totalAmount,
				durationMs: shownGroup.durationMs,
				remainingMs: shownGroup.renderRemainingMs,
				sourceName: shownGroup.count === 1 ? sourceSkillName(shownGroup.applications[0].sourceId) : undefined,
				applications:
					shownGroup.count > 1
						? shownGroup.applications.map((a) => ({ amount: a.amount, sourceName: sourceSkillName(a.sourceId) }))
						: undefined
			}
		: undefined
);

// Close the tooltip when the chip it's anchored to expires under the cursor (no `mouseleave` fires).
$effect(() => {
	if (shownKey != null && !shownGroup) {
		hideChipTooltip();
	}
});

// The transparent (revealed) arc of the radial overlay: the render-interpolated shared remaining as a
// fraction of the most-recent application's duration, in degrees. Depletes toward 0 as the stack
// expires; jumps back toward 360 when a fresh application resets the shared `renderRemainingMs`.
const remainingSweep = (group: EffectGroup) =>
	group.durationMs > 0 ? Math.max(0, Math.min(360, (group.renderRemainingMs / group.durationMs) * 360)) : 0;

const showChipTooltip = (group: EffectGroup, anchor: TooltipAnchor) => {
	shownKey = group.key;
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
	shownKey = undefined;
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
