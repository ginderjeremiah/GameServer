<div class="tt-mods-list">
	{#each slots as slot (slot.slotId)}
		{#if slot.mod}
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
import { modTypeColor, modTypeLabel, rarityColor } from '$lib/common';

interface ModSlotView {
	slotId: number;
	/** The slot's mod-type id (drives the accent and label). */
	type: number;
	/** The applied mod, or null for an empty slot. */
	mod: ItemMod | null;
}

interface Props {
	slots: ModSlotView[];
}

const { slots }: Props = $props();
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
</style>
