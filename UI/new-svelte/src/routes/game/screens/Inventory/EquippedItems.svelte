<div class="equipped-slots-container">
	<div class="equipped-helm-container">
		<ItemSlot
			slot={helmSlot}
			onMouseMove={handleMouseMove}
			onMouseEnter={(ev) => handleMouseEnter(ev, helmSlot)}
			onMouseLeave={(ev) => handleMouseLeave(ev, helmSlot)}
			onDragStart={hideTooltip}
			onDrop={hideTooltip}
		/>
	</div>
	<div class="equipped-chest-container">
		<ItemSlot
			slot={chestSlot}
			onMouseMove={handleMouseMove}
			onMouseEnter={(ev) => handleMouseEnter(ev, chestSlot)}
			onMouseLeave={(ev) => handleMouseLeave(ev, chestSlot)}
			onDragStart={hideTooltip}
			onDrop={hideTooltip}
		/>
	</div>
	<div class="equipped-leg-container">
		<ItemSlot
			slot={legSlot}
			onMouseMove={handleMouseMove}
			onMouseEnter={(ev) => handleMouseEnter(ev, legSlot)}
			onMouseLeave={(ev) => handleMouseLeave(ev, legSlot)}
			onDragStart={hideTooltip}
			onDrop={hideTooltip}
		/>
	</div>
	<div class="equipped-boot-container">
		<ItemSlot
			slot={bootSlot}
			onMouseMove={handleMouseMove}
			onMouseEnter={(ev) => handleMouseEnter(ev, bootSlot)}
			onMouseLeave={(ev) => handleMouseLeave(ev, bootSlot)}
			onDragStart={hideTooltip}
			onDrop={hideTooltip}
		/>
	</div>
	<div class="equipped-weapon-container">
		<ItemSlot
			slot={weaponSlot}
			onMouseMove={handleMouseMove}
			onMouseEnter={(ev) => handleMouseEnter(ev, weaponSlot)}
			onMouseLeave={(ev) => handleMouseLeave(ev, weaponSlot)}
			onDragStart={hideTooltip}
			onDrop={hideTooltip}
		/>
	</div>
	<div class="equipped-accessory-container">
		<ItemSlot
			slot={accessorySlot}
			onMouseMove={handleMouseMove}
			onMouseEnter={(ev) => handleMouseEnter(ev, accessorySlot)}
			onMouseLeave={(ev) => handleMouseLeave(ev, accessorySlot)}
			onDragStart={hideTooltip}
			onDrop={hideTooltip}
		/>
	</div>
	<ItemTooltip bind:this={tooltip} slot={tooltipSlot} />
</div>

<script lang="ts">
import ItemSlot from './ItemSlot.svelte';
import { registerTooltipComponent, type TooltipComponent } from '$stores';
import ItemTooltip from './ItemTooltip.svelte';
import { EEquipmentSlot, inventoryManager, type InventorySlot } from '$lib/engine';

let tooltip = $state<TooltipComponent>();
let tooltipSlot = $state<InventorySlot>();

const equippedItems = $derived(inventoryManager.equippedSlots);
const helmSlot = $derived(equippedItems[EEquipmentSlot.HelmSlot]);
const chestSlot = $derived(equippedItems[EEquipmentSlot.ChestSlot]);
const legSlot = $derived(equippedItems[EEquipmentSlot.LegSlot]);
const bootSlot = $derived(equippedItems[EEquipmentSlot.BootSlot]);
const weaponSlot = $derived(equippedItems[EEquipmentSlot.WeaponSlot]);
const accessorySlot = $derived(equippedItems[EEquipmentSlot.AccessorySlot]);

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
</script>

<style lang="scss">
.equipped-slots-container {
	margin: 0 0 1em 5%;
	display: block;
	width: 25%;
	border: var(--default-border);
	border-width: 3px;
	border-radius: 1vw;
	background-color: var(--container-background-color);
	position: relative;
	height: 30vw;

	.equipped-helm-container {
		position: absolute;
		left: calc(50% - var(--slot-width) / 2);
		top: 2.2%;
	}

	.equipped-chest-container {
		position: absolute;
		left: calc(50% - var(--slot-width) / 2);
		top: calc(var(--slot-width) + 4.4%);
	}

	.equipped-leg-container {
		position: absolute;
		left: calc(50% - var(--slot-width) / 2);
		top: calc(var(--slot-width) * 2 + 6.6%);
	}

	.equipped-boot-container {
		position: absolute;
		left: calc(50% - var(--slot-width) / 2);
		top: calc(var(--slot-width) * 3 + 8.8%);
	}

	.equipped-weapon-container {
		position: absolute;
		left: calc(46% - var(--slot-width) * 1.5);
		top: calc(var(--slot-width) * 1.5 + 5.5%);
	}

	.equipped-accessory-container {
		position: absolute;
		left: calc(54% + var(--slot-width) / 2);
		top: calc(var(--slot-width) * 1.5 + 5.5%);
	}
}
</style>
