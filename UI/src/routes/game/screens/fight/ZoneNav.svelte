<div class="zone-nav" data-testid="zone-nav">
	<button
		class="zone-btn"
		class:locked={leftLocked}
		disabled={leftDisabled}
		aria-label={leftLocked ? 'Previous zone locked' : 'Previous zone'}
		title={leftLocked ? LOCK_HINT : undefined}
		onclick={handleClickLeft}
	>
		{#if leftLocked}<LockGlyph />{:else}&#8249;{/if}
	</button>
	<div class="zone-info">
		<span class="zone-num">Zone · {String(zoneNum).padStart(2, '0')}</span>
		<span class="zone-name">{current?.name}</span>
	</div>
	<button
		class="zone-btn"
		class:locked={rightLocked}
		disabled={rightDisabled}
		aria-label={rightLocked ? 'Next zone locked' : 'Next zone'}
		title={rightLocked ? LOCK_HINT : undefined}
		onclick={handleClickRight}
	>
		{#if rightLocked}<LockGlyph />{:else}&#8250;{/if}
	</button>
</div>

<script lang="ts">
import type { IZone } from '$lib/api';
import { staticData, playerChallenges } from '$stores';
import { playerManager } from '$lib/engine';
import { isZoneUnlocked, zonesByOrder } from '$lib/common';
import LockGlyph from './LockGlyph.svelte';

const LOCK_HINT = "Clear this zone's boss to unlock";

const orderedZones = $derived(zonesByOrder(staticData.zones ?? []));
const currentIndex = $derived(orderedZones.findIndex((z) => z.id === playerManager.currentZone));
// Fall back to the first ordered zone when the current zone is unknown (e.g. before data loads).
const resolvedIndex = $derived(currentIndex >= 0 ? currentIndex : 0);
const current = $derived(orderedZones[resolvedIndex]);
const zoneNum = $derived(resolvedIndex + 1);

const prevZone = $derived(resolvedIndex > 0 ? orderedZones[resolvedIndex - 1] : undefined);
const nextZone = $derived(resolvedIndex < orderedZones.length - 1 ? orderedZones[resolvedIndex + 1] : undefined);

const completed = (id: number) => playerChallenges.isChallengeCompleted(id);
// A neighbouring zone that exists but hasn't been unlocked yet is "locked" — visibly distinct from
// simply being at the first/last zone (no neighbour at all).
const leftLocked = $derived(prevZone != null && !isZoneUnlocked(prevZone, completed));
const rightLocked = $derived(nextZone != null && !isZoneUnlocked(nextZone, completed));
const leftDisabled = $derived(prevZone == null || leftLocked);
const rightDisabled = $derived(nextZone == null || rightLocked);

const goToZone = (zone: IZone | undefined) => {
	if (zone != null && isZoneUnlocked(zone, completed)) {
		playerManager.currentZone = zone.id;
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

	&:hover:not(:disabled) {
		background: var(--border-subtle);
	}

	&:disabled {
		opacity: 0.4;
		cursor: not-allowed;
	}

	// A locked neighbour (boss not yet cleared) reads more clearly than a plain end-of-list disabled
	// arrow, so the lock glyph stays legible.
	&.locked:disabled {
		opacity: 0.6;
		color: color-mix(in srgb, var(--text-primary) 65%, transparent);
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
