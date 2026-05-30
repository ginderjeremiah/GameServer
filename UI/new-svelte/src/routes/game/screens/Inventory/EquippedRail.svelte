<div class="rail">
	<div class="section-rule">
		<span class="diamond"></span>
		<span class="mono-label">Equipped</span>
		<div class="line"></div>
	</div>

	<div class="rail-body">
		{#each EQUIP_GROUPS as group}
			{@const slots = EQUIP_SLOTS.filter((s) => s.group === group.key)}
			{#if slots.length}
				<div class="group">
					<div class="group-label">{group.label}</div>
					<div class="group-slots">
						{#each slots as slot}
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

	<ItemTooltip bind:this={tooltip} item={tooltipItem} />
</div>

<script lang="ts">
import ItemTooltip from './ItemTooltip.svelte';
import EquipSlot from './EquipSlot.svelte';
import { registerTooltipComponent, type TooltipComponent } from '$stores';
import type { Item } from '$lib/battle';
import { EQUIP_GROUPS, EQUIP_SLOTS, type InventoryView } from './inventory-view.svelte';

const { view }: { view: InventoryView } = $props();

let tooltip = $state<TooltipComponent>();
let tooltipItem = $state<Item>();

const { setTooltipPosition, showTooltip, hideTooltip } = registerTooltipComponent(() => tooltip);

const handleHoverEnter = (item: Item, ev: MouseEvent) => {
	tooltipItem = item;
	setTooltipPosition({ x: ev.clientX, y: ev.clientY });
	showTooltip();
};
const handleHoverMove = (ev: MouseEvent) => setTooltipPosition({ x: ev.clientX, y: ev.clientY });
const handleHoverLeave = () => {
	tooltipItem = undefined;
	hideTooltip();
};

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
	background: rgba(20, 21, 27, 0.5);
	border: 1px solid rgba(255, 255, 255, 0.08);
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
	box-shadow: 0 0 6px rgba(161, 194, 247, 0.7);
	flex-shrink: 0;
}

.mono-label {
	font-family: 'Geist Mono', monospace;
	font-size: 9.5px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: rgba(240, 240, 240, 0.4);
}

.line {
	flex: 1;
	height: 1px;
	background: rgba(255, 255, 255, 0.08);
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
	font-family: 'Geist Mono', monospace;
	font-size: 9px;
	letter-spacing: 1.6px;
	text-transform: uppercase;
	color: rgba(240, 240, 240, 0.55);
}

.group-slots {
	display: flex;
	flex-direction: column;
	gap: 9px;
}

.loadout-footer {
	margin-top: 14px;
	padding-top: 14px;
	border-top: 1px solid rgba(255, 255, 255, 0.08);
}

.loadout-button {
	width: 100%;
	padding: 7px 0;
	font-family: Geist, sans-serif;
	font-size: 11.5px;
	color: rgba(240, 240, 240, 0.4);
	background: transparent;
	border: 1px dashed rgba(255, 255, 255, 0.14);
	border-radius: 2px;
	cursor: not-allowed;
}
</style>
