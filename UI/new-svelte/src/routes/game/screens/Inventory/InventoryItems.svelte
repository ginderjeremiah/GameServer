<div class="inventory-items-container">
	<div class="inventory-items-inner">
		<div class="inventory-grid">
			{#each unlockedItems as item}
				<ItemSlot
					{item}
					onClick={() => handleItemClick(item)}
					onMouseMove={handleMouseMove}
					onMouseEnter={(ev) => handleMouseEnter(ev, item)}
					onMouseLeave={() => handleMouseLeave(item)}
				/>
			{/each}
		</div>
		{#if unlockedItems.length === 0}
			<div class="empty-message">No items unlocked yet. Complete challenges to unlock items!</div>
		{/if}
	</div>
	<ItemTooltip bind:this={tooltip} {item} />
</div>

<script lang="ts">
import { inventoryManager, getEquipmentSlotForCategory } from '$lib/engine';
import { registerTooltipComponent, type TooltipComponent } from '$stores';
import ItemSlot from './ItemSlot.svelte';
import ItemTooltip from './ItemTooltip.svelte';
import type { Item as ItemType } from '$lib/battle';

let tooltip = $state<TooltipComponent>();
let item = $state<ItemType>();
let activeItem = $state<ItemType>();

const unlockedItems = $derived(inventoryManager.unlockedItemList);

const { setTooltipPosition, showTooltip, hideTooltip } = registerTooltipComponent(() => tooltip);

const handleMouseMove = (ev: MouseEvent) => {
	showTooltip();
	setTooltipPosition({ x: ev.clientX, y: ev.clientY });
};

const handleMouseEnter = (ev: MouseEvent, hoverItem: ItemType) => {
	activeItem = hoverItem;
	item = hoverItem;
	setTooltipPosition({ x: ev.clientX, y: ev.clientY });
	showTooltip();
};

const handleMouseLeave = (hoverItem: ItemType) => {
	if (activeItem === hoverItem) {
		activeItem = undefined;
		item = undefined;
		hideTooltip();
	}
};

const handleItemClick = async (clickedItem: ItemType) => {
	hideTooltip();
	if (clickedItem.equipped) {
		// Unequip if already equipped
		if (clickedItem.equipmentSlotId != null) {
			await inventoryManager.unequipItem(clickedItem.equipmentSlotId);
		}
	} else {
		// Equip to the matching slot
		const slotId = getEquipmentSlotForCategory(clickedItem.itemCategoryId);
		await inventoryManager.equipItem(clickedItem.itemId, slotId);
	}
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
		width: 100%;

		.inventory-grid {
			display: flex;
			flex-wrap: wrap;
			gap: 2px;
		}

		.empty-message {
			text-align: center;
			color: var(--text-color);
			opacity: 0.6;
			padding: 2rem;
			font-size: 0.9rem;
		}
	}
}
</style>
