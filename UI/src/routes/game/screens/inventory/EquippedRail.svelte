<div class="rail">
	<div class="section-rule">
		<span class="diamond"></span>
		<span class="mono-label">Equipped</span>
		<div class="line"></div>
	</div>

	<div class="rail-body">
		{#each EQUIP_GROUPS as group (group.key)}
			{@const slots = EQUIP_SLOTS.filter((s) => s.group === group.key)}
			{#if slots.length}
				<div class="group">
					<div class="group-label">{group.label}</div>
					<div class="group-slots">
						{#each slots as slot (slot.id)}
							<EquipSlot
								{slot}
								item={view.equippedBySlot[slot.id]}
								dragItem={view.dragItem}
								selected={!!view.selected && view.equippedBySlot[slot.id]?.itemId === view.selected.itemId}
								onSelect={(item) => view.select(item.itemId)}
								onDrop={handleDrop}
								onUnequip={(slotId) => view.unequip(slotId)}
								onHoverEnter={handleHoverEnter}
								onHoverMove={handleHoverMove}
								onHoverLeave={handleHoverLeave}
							/>
						{/each}
					</div>
				</div>
			{/if}
		{/each}
	</div>

	<div class="loadout-footer">
		<button class="loadout-button" title="Saved loadouts — coming soon" disabled>+ Save loadout</button>
	</div>
</div>

<script lang="ts">
import EquipSlot from './EquipSlot.svelte';
import type { Item } from '$lib/battle';
import { EQUIP_GROUPS, EQUIP_SLOTS, type InventoryView } from './inventory-view.svelte';
import { getItemTooltip } from './item-tooltip.svelte';

const { view }: { view: InventoryView } = $props();

const tooltip = getItemTooltip();

const handleHoverEnter = (item: Item, ev: MouseEvent) => tooltip?.show(item, ev);
const handleHoverMove = (ev: MouseEvent) => tooltip?.move(ev);
const handleHoverLeave = () => tooltip?.hide();

const handleDrop = (slotId: number) => {
	const dragged = view.dragItem;
	const slot = EQUIP_SLOTS.find((s) => s.id === slotId);
	if (dragged && slot && slot.category === dragged.itemCategoryId) {
		view.equip(dragged.itemId, slotId);
	}
};
</script>

<style lang="scss">
.rail {
	width: 300px;
	flex-shrink: 0;
	display: flex;
	flex-direction: column;
	background: color-mix(in srgb, var(--surface) 50%, transparent);
	border: 1px solid var(--border-subtle);
	border-radius: 4px;
	padding: 16px;
	position: relative;
}

.section-rule {
	display: flex;
	align-items: center;
	gap: 8px;
	margin-bottom: 14px;
}

.diamond {
	width: 5px;
	height: 5px;
	transform: rotate(45deg);
	background: var(--accent);
	box-shadow: 0 0 6px color-mix(in srgb, var(--accent) 70%, transparent);
	flex-shrink: 0;
}

.line {
	flex: 1;
	height: 1px;
	background: var(--border-subtle);
}

.rail-body {
	flex: 1;
	min-height: 0;
	overflow-y: auto;
	display: flex;
	flex-direction: column;
	gap: 16px;
}

.group-label {
	margin-bottom: 8px;
	font-family: var(--mono);
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: var(--text-tertiary);
}

.group-slots {
	display: flex;
	flex-direction: column;
	gap: 9px;
}

.loadout-footer {
	margin-top: 14px;
	padding-top: 14px;
	border-top: 1px solid var(--border-subtle);
}

.loadout-button {
	width: 100%;
	padding: 7px 0;
	font-family: Geist, sans-serif;
	font-size: 11.5px;
	color: var(--text-muted);
	background: transparent;
	border: 1px dashed var(--border-light);
	border-radius: 2px;
	cursor: not-allowed;
}
</style>
