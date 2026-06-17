<div class="tt-mods-list">
	{#each slots as slot (slot.slotId)}
		{#if masked}
			<!-- Sealed teaser: one redacted row per slot, so the *count* still reads true. -->
			<div class="sealed-slot" style:border-left="2px solid {tintColor(accent, 0.5)}">
				<svg width="10" height="10" viewBox="0 0 16 16" fill="none" stroke="var(--text-muted)" stroke-width="1.5">
					<rect x="3.5" y="7" width="9" height="6.5" rx="1" />
					<path d="M5.5 7V5.2a2.5 2.5 0 0 1 5 0V7" />
				</svg>
				<span class="sealed-slot-label">Sealed slot</span>
			</div>
		{:else if slot.mod}
			<div class="tt-mod-tile" style:border-left-color={rarityColor(slot.mod.rarityId)}>
				<div class="tt-mod-header">
					<span class="tt-mod-name" style:color={rarityColor(slot.mod.rarityId)}>{slot.mod.name}</span>
					<span class="tt-mod-type" style:color={modTypeColor(slot.type)}>{modTypeLabel(slot.type)}</span>
				</div>
				<div class="tt-mod-desc">{slot.mod.description}</div>
			</div>
		{:else}
			<div class="tt-mod-empty" style:border-left-color={modTypeColor(slot.type)}>
				<span class="tt-mod-empty-label">Empty slot</span>
				<span class="tt-mod-type" style:color={modTypeColor(slot.type)}>{modTypeLabel(slot.type)}</span>
			</div>
		{/if}
	{/each}
</div>

<script lang="ts">
import type { ItemMod } from '$lib/battle';
import { modTypeColor, modTypeLabel, rarityColor, tintColor } from '$lib/common';

interface ModSlotView {
	slotId: number;
	/** The slot's mod-type id (drives the accent and label). */
	type: number;
	/** The applied mod, or null for an empty slot. */
	mod: ItemMod | null;
}

interface Props {
	slots: ModSlotView[];
	/** Render every slot as a sealed teaser row instead of its real mod/empty state. */
	masked?: boolean;
	/** Accent hue tinting the sealed-slot rows. Only used when `masked`. */
	accent?: string;
}

const { slots, masked = false, accent = 'var(--accent)' }: Props = $props();
</script>

<style lang="scss">
.tt-mods-list {
	display: flex;
	flex-direction: column;
	gap: 6px;
}

.tt-mod-tile {
	padding: 6px 10px;
	background: color-mix(in srgb, var(--white) 3%, transparent);
	border-left: 2px solid;
}

.tt-mod-empty {
	padding: 6px 10px;
	border: 1px dashed var(--border-light);
	border-left: 2px solid;
	display: flex;
	align-items: center;
	gap: 8px;
}

.tt-mod-empty-label {
	font-size: 11.5px;
	font-style: italic;
	color: color-mix(in srgb, var(--text-primary) 50%, transparent);
}

.tt-mod-header {
	display: flex;
	align-items: baseline;
	gap: 8px;
	margin-bottom: 2px;
}

.tt-mod-name {
	font-size: 12px;
	font-weight: 500;
	color: var(--text-primary);
}

.tt-mod-type {
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.2px;
	text-transform: uppercase;
}

.tt-mod-desc {
	font-size: 11.5px;
	color: color-mix(in srgb, var(--text-primary) 65%, transparent);
	line-height: 1.5;
}

.sealed-slot {
	padding: 6px 10px;
	border: 1px dashed var(--border-light);
	display: flex;
	align-items: center;
	gap: 8px;
}

.sealed-slot-label {
	font-size: 11.5px;
	color: color-mix(in srgb, var(--text-primary) 45%, transparent);
	font-style: italic;
}
</style>
