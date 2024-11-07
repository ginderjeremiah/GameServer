<div class="inventory-items-container">
	<div class="inventory-items-inner">
		<div class="inventory-slots-subcontainer">
			{#each inventoryItems as slot, index}
				<ItemSlot
					{slot}
					hideBottomBorder={index < 18}
					hideRightBorder={index % 9 !== 8}
					onDragStart={hideTooltip}
					onDrop={hideTooltip}
					onClick={handleClick}
					onMouseMove={handleMouseMove}
					onMouseEnter={(ev) => handleMouseEnter(ev, slot)}
					onMouseLeave={(ev) => handleMouseLeave(ev, slot)}
				/>
			{/each}
		</div>
		<div class="inventory-bottom">
			<ItemSlot
				slot={inventory.trashSlot}
				undraggable
				onDrop={hideTooltip}
				onMouseMove={handleMouseMove}
				onMouseEnter={(ev) => handleMouseEnter(ev, inventory.trashSlot)}
				onMouseLeave={(ev) => handleMouseLeave(ev, inventory.trashSlot)}
			/>
		</div>
	</div>
	<ItemTooltip bind:this={tooltip} slot={tooltipSlot} />
</div>

<script lang="ts">
import {
	inventory,
	registerTooltipComponent,
	type TooltipComponent,
	type InventorySlot,
	swapSlots
} from '$stores';
import ItemSlot from './ItemSlot.svelte';
import ItemTooltip from './ItemTooltip.svelte';

let tooltip = $state<TooltipComponent>();
let tooltipSlot = $state<InventorySlot>();

const inventoryItems = $derived(inventory.slots);

const { setTooltipPosition, showTooltip, hideTooltip } = registerTooltipComponent(() => tooltip);

const handleMouseMove = (ev: MouseEvent) => {
	showTooltip();
	setTooltipPosition({ x: ev.clientX, y: ev.clientY });
};

const handleMouseEnter = (ev: MouseEvent, slot: InventorySlot) => {
	tooltipSlot = slot;
	if (slot.item) {
		setTooltipPosition({ x: ev.clientX, y: ev.clientY });
		showTooltip();
	}
};

const handleMouseLeave = (ev: MouseEvent, slot: InventorySlot) => {
	if (tooltipSlot === slot) {
		tooltipSlot = undefined;
		hideTooltip();
	}
};

const handleClick = (ev: MouseEvent, slot: InventorySlot) => {
	if (ev.shiftKey) {
		equipItemInSlot(slot);
	} else if (ev.ctrlKey) {
		deleteItemInSlot(slot);
	}
};

const equipItemInSlot = (slot: InventorySlot) => {
	if (slot.item) {
		const newSlot = inventory.equippedSlots.find((s) => s.canHold(slot.item));
		if (newSlot) {
			swapSlots(slot, newSlot);
		}
	}
};

const deleteItemInSlot = (slot: InventorySlot) => {
	swapSlots(slot, inventory.trashSlot);
	hideTooltip();
};
</script>

<style lang="scss">
.inventory-items-container {
	margin: 0 5% 1rem 0;
	width: 60%;
	padding: 1rem;
	display: block;
	border: var(--default-border);
	border-width: 3px;
	border-radius: 1vw;
	background-color: var(--container-background-color);
	position: relative;

	.inventory-items-inner {
		margin: 0 auto;
		height: 100%;
		width: fit-content;
		display: flex;
		flex-direction: column;
		justify-content: space-between;

		.inventory-slots-subcontainer {
			width: calc(9 * var(--slot-width));
			display: flex;
			flex-wrap: wrap;
		}

		.inventory-bottom {
			display: flex;
			justify-content: flex-end;
		}
	}
}
</style>
