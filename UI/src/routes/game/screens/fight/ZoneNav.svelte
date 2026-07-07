<div class="zone-nav" data-testid="zone-nav">
	<!-- svelte-ignore a11y_no_static_element_interactions -->
	<span
		class="zone-btn-wrap"
		onmouseenter={(e) => showLock(leftLocked, prevZone?.unlockChallengeId, e)}
		onmousemove={moveLock}
		onmouseleave={hideLock}
	>
		<button
			class="zone-btn"
			class:locked={leftLocked}
			disabled={leftAbsent}
			aria-disabled={leftLocked || undefined}
			aria-label={bossEngaged
				? 'Zone navigation locked during boss fight'
				: leftLocked
					? 'Previous zone locked'
					: 'Previous zone'}
			onclick={handleClickLeft}
			onfocus={(e) => focusLock(leftLocked, prevZone?.unlockChallengeId, e)}
			onblur={hideLock}
			use:describedByTooltip={leftLocked ? describedById : undefined}
		>
			{#if leftLocked}<LockGlyph />{:else}&#8249;{/if}
		</button>
	</span>
	<div class="zone-info">
		{#if atHome}
			<span class="zone-num">Sanctuary</span>
		{:else}
			<span class="zone-num">Zone · {String(combatZoneNum).padStart(2, '0')}</span>
		{/if}
		<span class="zone-name">{current?.name}</span>
	</div>
	<!-- svelte-ignore a11y_no_static_element_interactions -->
	<span
		class="zone-btn-wrap"
		onmouseenter={(e) => showLock(rightLocked, nextZone?.unlockChallengeId, e)}
		onmousemove={moveLock}
		onmouseleave={hideLock}
	>
		<button
			class="zone-btn"
			class:locked={rightLocked}
			disabled={rightAbsent}
			aria-disabled={rightLocked || undefined}
			aria-label={bossEngaged
				? 'Zone navigation locked during boss fight'
				: rightLocked
					? 'Next zone locked'
					: 'Next zone'}
			onclick={handleClickRight}
			onfocus={(e) => focusLock(rightLocked, nextZone?.unlockChallengeId, e)}
			onblur={hideLock}
			use:describedByTooltip={rightLocked ? describedById : undefined}
		>
			{#if rightLocked}<LockGlyph />{:else}&#8250;{/if}
		</button>
	</span>
</div>

<ChallengeTooltip bind:this={tooltip} {challengeId} />

<script lang="ts">
import type { IZone } from '$lib/api';
import {
	anchorPosition,
	focusAnchor,
	staticData,
	playerChallenges,
	registerTooltipComponent,
	type TooltipAnchor,
	type TooltipComponent
} from '$stores';
import { enemyManager, playerManager } from '$lib/engine';
import { isZoneUnlocked, navigableZones } from '$lib/common';
import { ChallengeTooltip } from '$components';
import { describedByTooltip } from '$components/tooltip/describedby-tooltip';
import LockGlyph from './LockGlyph.svelte';

// A locked zone is always gated on a challenge (an ungated zone is never locked), so the lock
// affordance shows that gating challenge — what it requires and what completing it unlocks —
// rather than a static hint that wrongly assumed every gate was the zone's boss. The explanation is
// reachable by both mouse hover and keyboard focus (the locked arrow stays focusable via
// `aria-disabled` rather than `disabled`).
let tooltip = $state<TooltipComponent>();
let challengeId = $state<number | undefined>();
const { describedById, setTooltipPosition, showTooltip, hideTooltip } = registerTooltipComponent(() => tooltip);

// Anchor the lock tooltip at the cursor (mouse) or off the focused arrow's box (focus).
const showLock = (locked: boolean, gateChallengeId: number | undefined, anchor: TooltipAnchor) => {
	if (!locked) {
		return;
	}
	challengeId = gateChallengeId;
	setTooltipPosition(anchorPosition(anchor));
	showTooltip();
};
const moveLock = (ev: MouseEvent) => setTooltipPosition(anchorPosition(ev));
// Keyboard focus anchors off the arrow's box; a mouse click is left to the hover handlers so the
// tooltip keeps tracking the cursor instead of jumping (#880).
const focusLock = (locked: boolean, gateChallengeId: number | undefined, ev: FocusEvent) => {
	const anchor = focusAnchor(ev);
	if (anchor) {
		showLock(locked, gateChallengeId, anchor);
	}
};
const hideLock = () => {
	hideTooltip();
	challengeId = undefined;
};

// Retired zones are out of circulation: exclude them so they're never the current zone or a
// navigable neighbour (the backend relocates a player who is somehow still in one).
const orderedZones = $derived(navigableZones(staticData.zones ?? []));
const currentIndex = $derived(orderedZones.findIndex((z) => z.id === playerManager.currentZone));
// Fall back to the first ordered zone when the current zone is unknown (e.g. before data loads).
const resolvedIndex = $derived(currentIndex >= 0 ? currentIndex : 0);
const current = $derived(orderedZones[resolvedIndex]);
// The no-combat Home sanctuary is a navigable zone but is labelled "Sanctuary", not "Zone · NN".
const atHome = $derived(current?.isHome === true);
// Number only the combat (non-Home) zones, so the sanctuary sitting leftmost doesn't push "Zone · 01"
// off the first real zone. A combat zone's number is its 1-based position among the navigable non-Home zones.
const combatZoneNum = $derived(orderedZones.slice(0, resolvedIndex + 1).filter((z) => !z.isHome).length);

const prevZone = $derived(resolvedIndex > 0 ? orderedZones[resolvedIndex - 1] : undefined);
const nextZone = $derived(resolvedIndex < orderedZones.length - 1 ? orderedZones[resolvedIndex + 1] : undefined);

const completed = (id: number) => playerChallenges.isChallengeCompleted(id);
// A neighbouring zone that exists but hasn't been unlocked yet is "locked" — visibly distinct from
// simply being at the first/last zone (no neighbour at all).
const leftLocked = $derived(prevZone != null && !isZoneUnlocked(prevZone, completed));
const rightLocked = $derived(nextZone != null && !isZoneUnlocked(nextZone, completed));
// A dedicated-boss fight is engaged, so navigation is hard-disabled: reassigning the current zone
// mid-fight would desync the boss UI (and the eventual clear/unlock check) from the fight actually
// in progress (#1698). Retreat is the sanctioned way out and already returns to idle before navigating.
const bossEngaged = $derived(enemyManager.mode === 'boss');
// An arrow is hard-`disabled` (unfocusable) when there is no neighbour to explain, or while a boss
// fight is engaged; a locked-but-existing neighbour otherwise stays focusable with `aria-disabled` so
// its gate is reachable.
const leftAbsent = $derived(prevZone == null || bossEngaged);
const rightAbsent = $derived(nextZone == null || bossEngaged);

const goToZone = (zone: IZone | undefined) => {
	if (zone != null && !bossEngaged && isZoneUnlocked(zone, completed)) {
		enemyManager.navigateToZone(zone.id);
	}
};

const handleClickLeft = () => goToZone(prevZone);
const handleClickRight = () => goToZone(nextZone);
</script>

<style lang="scss">
.zone-nav {
	display: inline-flex;
	align-items: center;
	gap: 12px;
	background: color-mix(in srgb, var(--white) 4%, transparent);
	border: 1px solid var(--border-light);
	border-radius: 3px;
	padding: 6px 10px 6px 6px;
}

.zone-btn-wrap {
	display: inline-flex;
}

.zone-btn {
	width: 24px;
	height: 24px;
	background: color-mix(in srgb, var(--white) 3%, transparent);
	border: 1px solid var(--border-medium);
	color: color-mix(in srgb, var(--text-primary) 85%, transparent);
	border-radius: 2px;
	cursor: pointer;
	display: flex;
	align-items: center;
	justify-content: center;
	font-family: var(--mono);
	font-size: 12px;
	transition: background 140ms;

	&:hover:not(:disabled):not(.locked) {
		background: var(--border-subtle);
	}

	&:focus-visible {
		outline: 2px solid var(--accent);
		outline-offset: 2px;
	}

	&:disabled {
		opacity: 0.4;
		cursor: not-allowed;
	}

	// A locked neighbour (gating challenge not yet completed) reads more clearly than a plain
	// end-of-list disabled arrow, so the lock glyph stays legible. It stays focusable (not
	// `disabled`, but `aria-disabled`) so the gate tooltip is keyboard-reachable; activation is a
	// no-op because `goToZone` only navigates to an unlocked zone.
	&.locked {
		opacity: 0.6;
		color: color-mix(in srgb, var(--text-primary) 65%, transparent);
		cursor: not-allowed;
	}
}

.zone-info {
	display: flex;
	align-items: baseline;
	gap: 10px;
	min-width: 200px;
	justify-content: center;
}

.zone-num {
	font-family: var(--mono);
	font-size: 10.5px;
	letter-spacing: 1.5px;
	text-transform: uppercase;
	color: color-mix(in srgb, var(--accent-light) 85%, transparent);
}

.zone-name {
	font-size: 16px;
	color: var(--text-primary);
	font-weight: 400;
	letter-spacing: 0.1px;
}
</style>
