<div class="equipped-slots-container">
	{#each slotEntries as [slotId, label]}
		<div class="equipped-slot">
			<div class="slot-label">{label}</div>
			<ItemSlot
				item={equippedSlots[slotId]}
				onClick={() => handleSlotClick(slotId)}
				onMouseMove={handleMouseMove}
				onMouseEnter={(ev) => handleMouseEnter(ev, slotId)}
				onMouseLeave={() => handleMouseLeave(slotId)}
			/>
		</div>
	{/each}
	<ItemTooltip bind:this={tooltip} item={tooltipItem} />
</div>

<script lang="ts">
import ItemSlot from './ItemSlot.svelte';
import { registerTooltipComponent, type TooltipComponent } from '$stores';
import ItemTooltip from './ItemTooltip.svelte';
import { EEquipmentSlot, inventoryManager } from '$lib/engine';
import type { Item } from '$lib/battle';

let tooltip = $state<TooltipComponent>();
let tooltipItem = $state<Item>();
let activeSlotId = $state<number>();

const equippedSlots = $derived(inventoryManager.equippedSlots);

const slotEntries: [EEquipmentSlot, string][] = [
	[EEquipmentSlot.HelmSlot, 'Helm'],
	[EEquipmentSlot.ChestSlot, 'Chest'],
	[EEquipmentSlot.LegSlot, 'Legs'],
	[EEquipmentSlot.BootSlot, 'Boots'],
	[EEquipmentSlot.WeaponSlot, 'Weapon'],
	[EEquipmentSlot.AccessorySlot, 'Accessory'],
];

const { setTooltipPosition, showTooltip, hideTooltip } = registerTooltipComponent(() => tooltip);

const handleMouseMove = (ev: MouseEvent) => {
	showTooltip();
	setTooltipPosition({ x: ev.clientX, y: ev.clientY });
};

const handleMouseEnter = (ev: MouseEvent, slotId: EEquipmentSlot) => {
	activeSlotId = slotId;
	const item = equippedSlots[slotId];
	if (item) {
		tooltipItem = item;
		setTooltipPosition({ x: ev.clientX, y: ev.clientY });
		showTooltip();
	}
};

const handleMouseLeave = (slotId: EEquipmentSlot) => {
	if (activeSlotId === slotId) {
		activeSlotId = undefined;
		tooltipItem = undefined;
		hideTooltip();
	}
};

const handleSlotClick = async (slotId: EEquipmentSlot) => {
	const item = equippedSlots[slotId];
	if (item) {
		hideTooltip();
		await inventoryManager.unequipItem(slotId);
	}
};
</script>

<style lang="scss">
.equipped-slots-container {
	margin: 0 0 1em 5%;
	display: flex;
	flex-direction: column;
	gap: 0.5em;
	width: 25%;
	border: var(--default-border);
	border-width: 3px;
	border-radius: 1vw;
	background-color: var(--container-background-color);
	padding: 1rem;
	position: relative;

	.equipped-slot {
		display: flex;
		align-items: center;
		gap: 0.5em;

		.slot-label {
			width: 5em;
			font-size: 0.8rem;
			text-align: right;
			color: var(--text-color);
		}
	}
}
</style>
